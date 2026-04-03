#if LINUX
namespace OmenKeyboardService;

/// <summary>
/// Writes HID commands to the keyboard via raw /dev/hidraw access.
/// Opens the device, writes all commands, and closes it — no persistent monitoring thread.
/// </summary>
public class LinuxKeyboardHidWriter : IKeyboardHidWriter
{
    private readonly ILogger<LinuxKeyboardHidWriter> _logger;

    // Report ID (0) + 64 bytes of command data
    private const int HidReportSize = 65;

    public LinuxKeyboardHidWriter(ILogger<LinuxKeyboardHidWriter> logger)
    {
        _logger = logger;
    }

    public void WriteCommands(byte[][] commands)
    {
        var devicePath = FindKeyboardHidrawPath();

        using var fs = new FileStream(devicePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        foreach (var command in commands)
        {
            var buffer = new byte[HidReportSize];
            buffer[0] = 0; // Report ID
            Array.Copy(command, 0, buffer, 1, Math.Min(command.Length, HidReportSize - 1));
            fs.Write(buffer);
            fs.Flush();
        }
    }

    // The vendor-specific usage page in the HID report descriptor that identifies
    // the LED control interface (as opposed to keyboard input or media key interfaces).
    // Byte sequence: 0x06 (Usage Page, 2-byte), 0x13, 0xFF → Usage Page 0xFF13.
    private static readonly byte[] VendorUsagePage = [0x06, 0x13, 0xFF];

    /// <summary>
    /// Finds the hidraw device path for the HP Omen keyboard's LED control interface by scanning sysfs.
    /// The keyboard exposes multiple HID interfaces (keyboard input, media keys, LED control) that all
    /// share the same VID/PID. We identify the correct one by its vendor-specific HID report descriptor.
    /// </summary>
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
                // HID_ID line format: HID_ID=0003:000003F0:00001F41
                if (!uevent.Contains(vendorHex, StringComparison.OrdinalIgnoreCase) ||
                    !uevent.Contains(productHex, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Multiple interfaces share the same VID/PID. Check the report descriptor
                // to find the vendor-specific interface used for LED control.
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

        throw new InvalidOperationException(
            $"HP Omen keyboard LED interface not found (VID: 0x{vendorHex}, PID: 0x{productHex}). " +
            "Please ensure the keyboard is connected and initialized.");
    }

    private bool HasVendorUsagePage(string reportDescriptorPath)
    {
        try
        {
            // sysfs files report a fixed block size (4096) via fstat regardless of actual content,
            // which causes File.ReadAllBytes to throw EndOfStreamException. Read manually instead.
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
