#if LINUX
namespace OmenKeyboardService;

/// <summary>
/// Writes HID commands to the keyboard via raw /dev/hidraw access.
/// LED commands use Report ID 0, macro commands use Report ID 1.
/// </summary>
public class LinuxKeyboardHidWriter : IKeyboardHidWriter
{
    private readonly ILogger<LinuxKeyboardHidWriter> _logger;

    private const int HidReportSize = 65; // Report ID (1 byte) + 64 bytes of command data

    private static readonly byte[] VendorUsagePage = [0x06, 0x13, 0xFF];

    public LinuxKeyboardHidWriter(ILogger<LinuxKeyboardHidWriter> logger)
    {
        _logger = logger;
    }

    // MCU acknowledgement bytes in the response payload (payload[4], payload[5]).
    private const byte AckByte4 = 0xEC;
    private const byte AckByte5 = 0xAC;
    private const int AckReadTimeoutMs = 500;

    public void WriteCommands(byte[][] commands, int[]? delaysBeforeMs = null)
    {
        var devicePath = FindKeyboardHidrawPath();

        using var fs = new FileStream(devicePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        for (int i = 0; i < commands.Length; i++)
        {
            int delay = delaysBeforeMs is not null && i < delaysBeforeMs.Length ? delaysBeforeMs[i] : 0;
            if (delay > 0)
                Thread.Sleep(delay);

            var writeBuffer = new byte[HidReportSize];
            writeBuffer[0] = 0; // Report ID 0 (LED)
            Array.Copy(commands[i], 0, writeBuffer, 1, Math.Min(commands[i].Length, HidReportSize - 1));
            fs.Write(writeBuffer);
            fs.Flush();

            VerifyAck(fs, i);
        }
    }

    /// <summary>
    /// Reads the MCU's response after a write and verifies the expected ACK bytes. hidraw reads
    /// are blocking, so we run the read on a background task and cancel via timeout.
    /// </summary>
    private static void VerifyAck(FileStream fs, int commandIndex)
    {
        var response = new byte[HidReportSize];
        var readTask = Task.Run(() => fs.Read(response, 0, response.Length));

        if (!readTask.Wait(TimeSpan.FromMilliseconds(AckReadTimeoutMs)))
            throw new KeyboardWriteVerificationException(commandIndex, null, $"Timed out waiting for MCU ACK on command {commandIndex}");

        int bytesRead = readTask.Result;
        if (bytesRead < 7)
            throw new KeyboardWriteVerificationException(commandIndex, response, $"Short MCU response on command {commandIndex}: {bytesRead} bytes");

        // response[0] is the Report ID byte, so payload[4] = response[5].
        byte payloadByte4 = response[5];
        byte payloadByte5 = response[6];
        if (payloadByte4 != AckByte4 || payloadByte5 != AckByte5)
            throw new KeyboardWriteVerificationException(commandIndex, response, $"MCU did not ACK command {commandIndex}: got [{payloadByte4:X2} {payloadByte5:X2}], expected [{AckByte4:X2} {AckByte5:X2}]");
    }

    public bool TryForceReenumeration()
    {
        // Linux programmatic USB re-enumeration is not implemented yet.
        // Possible approach: echo the device id into /sys/bus/usb/drivers/usb/unbind then /bind.
        _logger.LogWarning("Force re-enumeration is not implemented on Linux");
        return false;
    }

    public byte[][] WriteAndReadAll(byte[][] commands, byte reportId = 0, int readTimeoutMs = 2000)
    {
        var devicePath = FindKeyboardHidrawPath();

        using var fs = new FileStream(devicePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        var responses = new byte[commands.Length][];

        for (int i = 0; i < commands.Length; i++)
        {
            var buffer = new byte[HidReportSize];
            buffer[0] = reportId;
            Array.Copy(commands[i], 0, buffer, 1, Math.Min(commands[i].Length, HidReportSize - 1));
            fs.Write(buffer);
            fs.Flush();

            Thread.Sleep(200);

            // hidraw reads are blocking — use a cancellation timeout to avoid hanging
            var response = new byte[HidReportSize];
            var readTask = Task.Run(() => fs.Read(response, 0, response.Length));
            if (!readTask.Wait(TimeSpan.FromSeconds(3)))
                throw new TimeoutException("HID read timed out — MCU did not respond within 3 seconds");
            int bytesRead = readTask.Result;

            var result = new byte[64];
            Array.Copy(response, 1, result, 0, Math.Min(bytesRead - 1, 64));
            responses[i] = result;
        }

        return responses;
    }

    private string FindKeyboardHidrawPath()
    {
        var vendorHex = OmenKeyboardConstants.VendorId.ToString("X4");
        var productHex = OmenKeyboardConstants.ProductId.ToString("X4");

        _logger.LogDebug("Searching for HP Omen keyboard LED interface (VID: 0x{VendorId}, PID: 0x{ProductId}) via sysfs...", vendorHex, productHex);

        var hidrawDirs = Directory.GetDirectories("/sys/class/hidraw");

        foreach (var hidrawDir in hidrawDirs)
        {
            var ueventPath = Path.Combine(hidrawDir, "device", "uevent");
            if (!File.Exists(ueventPath))
                continue;

            try
            {
                var uevent = File.ReadAllText(ueventPath);
                if (!uevent.Contains(vendorHex, StringComparison.OrdinalIgnoreCase) || !uevent.Contains(productHex, StringComparison.OrdinalIgnoreCase))
                    continue;

                var descriptorPath = Path.Combine(hidrawDir, "device", "report_descriptor");
                if (!HasVendorUsagePage(descriptorPath))
                {
                    _logger.LogDebug("Skipping {Device} — not the LED control interface", Path.GetFileName(hidrawDir));
                    continue;
                }

                var deviceName = Path.GetFileName(hidrawDir);
                var devicePath = Path.Combine("/dev", deviceName);

                if (File.Exists(devicePath))
                {
                    _logger.LogDebug("Found keyboard LED interface at {DevicePath}", devicePath);
                    return devicePath;
                }
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Could not read sysfs for {Path}, skipping", hidrawDir);
            }
        }

        throw new InvalidOperationException($"HP Omen keyboard LED interface not found (VID: 0x{vendorHex}, PID: 0x{productHex}). Please ensure the keyboard is connected and initialized.");
    }

    private bool HasVendorUsagePage(string reportDescriptorPath)
    {
        try
        {
            using var fs = new FileStream(reportDescriptorPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[4096];
            int bytesRead = fs.Read(buffer, 0, buffer.Length);
            return buffer.AsSpan(0, bytesRead).IndexOf(VendorUsagePage) >= 0;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
#endif
