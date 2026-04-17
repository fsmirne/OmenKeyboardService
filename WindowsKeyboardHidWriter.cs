#if WINDOWS
using System.Runtime.Versioning;
using HidSharp;

namespace OmenKeyboardService;

/// <summary>
/// Writes HID commands to the keyboard via HidSharp (Windows HID API wrapper).
/// LED commands use Report ID 0, macro commands use Report ID 1.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsKeyboardHidWriter : IKeyboardHidWriter
{
    private readonly ILogger<WindowsKeyboardHidWriter> _logger;

    private static readonly byte[] VendorUsagePage = [0x06, 0x13, 0xFF];

    public WindowsKeyboardHidWriter(ILogger<WindowsKeyboardHidWriter> logger)
    {
        _logger = logger;
    }

    // MCU acknowledgement bytes in the response payload (payload[4], payload[5]).
    // Sourced from HP's McuKeyboardLightingHelper.CheckResult (decompiled SDK).
    private const byte AckByte4 = 0xEC;
    private const byte AckByte5 = 0xAC;
    private const int AckReadTimeoutMs = 500;

    public void WriteCommands(byte[][] commands, int[]? delaysBeforeMs = null)
    {
        var device = FindKeyboardDevice();
        using var stream = device.Open();
        stream.ReadTimeout = AckReadTimeoutMs;

        int writeSize = device.GetMaxOutputReportLength();
        int readSize = device.GetMaxInputReportLength();

        for (int i = 0; i < commands.Length; i++)
        {
            int delay = delaysBeforeMs is not null && i < delaysBeforeMs.Length ? delaysBeforeMs[i] : 0;
            if (delay > 0)
                Thread.Sleep(delay);

            var writeBuffer = new byte[writeSize];
            writeBuffer[0] = 0; // Report ID 0 (LED)
            Array.Copy(commands[i], 0, writeBuffer, 1, Math.Min(commands[i].Length, writeSize - 1));
            stream.Write(writeBuffer);

            VerifyAck(stream, readSize, i);
        }
    }

    /// <summary>
    /// Reads the MCU's response after a write and verifies the expected ACK bytes. Throws
    /// <see cref="KeyboardWriteVerificationException"/> on timeout or mismatch.
    /// </summary>
    private static void VerifyAck(HidStream stream, int readSize, int commandIndex)
    {
        var readBuffer = new byte[readSize];
        int bytesRead;
        try
        {
            bytesRead = stream.Read(readBuffer);
        }
        catch (TimeoutException ex)
        {
            throw new KeyboardWriteVerificationException(commandIndex, null, $"Timed out waiting for MCU ACK on command {commandIndex}", ex);
        }

        // Strip the report ID byte to get the 64-byte payload.
        if (bytesRead < 7)
            throw new KeyboardWriteVerificationException(commandIndex, readBuffer, $"Short MCU response on command {commandIndex}: {bytesRead} bytes");

        byte payloadByte4 = readBuffer[5]; // readBuffer[0]=reportId, so payload[4]=readBuffer[5]
        byte payloadByte5 = readBuffer[6];
        if (payloadByte4 != AckByte4 || payloadByte5 != AckByte5)
            throw new KeyboardWriteVerificationException(commandIndex, readBuffer, $"MCU did not ACK command {commandIndex}: got [{payloadByte4:X2} {payloadByte5:X2}], expected [{AckByte4:X2} {AckByte5:X2}]");
    }

    public bool TryForceReenumeration()
    {
        _logger.LogInformation("Forcing USB re-enumeration of HP Omen keyboard (VID: 0x{VendorId:X4}, PID: 0x{ProductId:X4})...", OmenKeyboardConstants.VendorId, OmenKeyboardConstants.ProductId);

        bool toggled;
        try
        {
            toggled = WindowsDeviceReenumerator.ToggleDevice(OmenKeyboardConstants.VendorId, OmenKeyboardConstants.ProductId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetupAPI call failed while forcing re-enumeration");
            return false;
        }

        if (!toggled)
        {
            _logger.LogWarning("No matching USB composite device was found to toggle");
            return false;
        }

        // Wait for HidSharp to see the freshly re-enumerated vendor interface.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                FindKeyboardDevice();
                // Device is visible, but give the MCU a moment to be ready for vendor commands.
                Thread.Sleep(1000);
                _logger.LogInformation("USB re-enumeration complete; keyboard is back on the bus");
                return true;
            }
            catch
            {
                Thread.Sleep(100);
            }
        }

        _logger.LogWarning("Keyboard did not reappear within 5 seconds after re-enumeration");
        return false;
    }

    public byte[][] WriteAndReadAll(byte[][] commands, byte reportId = 0, int readTimeoutMs = 2000)
    {
        var device = FindKeyboardDevice();
        using var stream = device.Open();
        stream.ReadTimeout = readTimeoutMs;

        var responses = new byte[commands.Length][];

        for (int i = 0; i < commands.Length; i++)
        {
            var buffer = new byte[65];
            buffer[0] = reportId;
            Array.Copy(commands[i], 0, buffer, 1, Math.Min(commands[i].Length, 64));

            stream.Write(buffer);
            Thread.Sleep(200);

            var response = new byte[65];
            int bytesRead = stream.Read(response);

            var result = new byte[64];
            Array.Copy(response, 1, result, 0, Math.Min(bytesRead - 1, 64));
            responses[i] = result;
        }

        return responses;
    }

    private HidDevice FindKeyboardDevice()
    {
        var devices = DeviceList.Local.GetHidDevices(OmenKeyboardConstants.VendorId, OmenKeyboardConstants.ProductId).ToList();

        _logger.LogDebug("Searching for HP Omen keyboard (VID: 0x{VendorId:X4}, PID: 0x{ProductId:X4})...", OmenKeyboardConstants.VendorId, OmenKeyboardConstants.ProductId);

        if (!devices.Any())
            throw new InvalidOperationException($"HP Omen keyboard not found (VID: 0x{OmenKeyboardConstants.VendorId:X4}, PID: 0x{OmenKeyboardConstants.ProductId:X4}). Please ensure the keyboard is connected and initialized.");

        var device = devices.FirstOrDefault(d => d.GetRawReportDescriptor().AsSpan().IndexOf(VendorUsagePage) >= 0)
            ?? throw new InvalidOperationException($"HP Omen keyboard vendor interface not found (usage page 0xFF13). Found {devices.Count} device(s) but none match.");

        _logger.LogDebug("Selected keyboard interface: {DevicePath}", device.DevicePath);
        return device;
    }
}
#endif
