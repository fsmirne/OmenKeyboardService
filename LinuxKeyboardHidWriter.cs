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

    /// <summary>
    /// Finds the hidraw device path for the HP Omen keyboard by scanning sysfs
    /// </summary>
    private string FindKeyboardHidrawPath()
    {
        var vendorHex = OmenKeyboardConstants.VendorId.ToString("X4");
        var productHex = OmenKeyboardConstants.ProductId.ToString("X4");

        _logger.LogDebug("Searching for HP Omen keyboard (VID: 0x{VendorId}, PID: 0x{ProductId}) via sysfs...", vendorHex, productHex);

        var hidrawDirs = Directory.GetDirectories("/sys/class/hidraw");
        Array.Sort(hidrawDirs);
        Array.Reverse(hidrawDirs);

        foreach (var hidrawDir in hidrawDirs)
        {
            var ueventPath = Path.Combine(hidrawDir, "device", "uevent");
            if (!File.Exists(ueventPath))
                continue;

            try
            {
                var uevent = File.ReadAllText(ueventPath);
                // HID_ID line format: HID_ID=0003:000003F0:00001F41
                if (uevent.Contains(vendorHex, StringComparison.OrdinalIgnoreCase) &&
                    uevent.Contains(productHex, StringComparison.OrdinalIgnoreCase))
                {
                    var deviceName = Path.GetFileName(hidrawDir);
                    var devicePath = Path.Combine("/dev", deviceName);

                    if (File.Exists(devicePath))
                    {
                        _logger.LogDebug("Found keyboard at {DevicePath}", devicePath);
                        return devicePath;
                    }
                }
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Could not read sysfs for {Path}, skipping", hidrawDir);
            }
        }

        throw new InvalidOperationException(
            $"HP Omen keyboard not found (VID: 0x{vendorHex}, PID: 0x{productHex}). " +
            "Please ensure the keyboard is connected and initialized.");
    }
}
#endif
