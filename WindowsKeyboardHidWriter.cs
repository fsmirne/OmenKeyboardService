#if WINDOWS
using HidSharp;

namespace OmenKeyboardService;

/// <summary>
/// Writes HID commands to the keyboard via HidSharp (Windows HID API wrapper)
/// </summary>
public class WindowsKeyboardHidWriter : IKeyboardHidWriter
{
    private readonly ILogger<WindowsKeyboardHidWriter> _logger;

    public WindowsKeyboardHidWriter(ILogger<WindowsKeyboardHidWriter> logger)
    {
        _logger = logger;
    }

    public void WriteCommands(byte[][] commands)
    {
        var device = OpenKeyboardDevice();
        using var stream = device.Open();

        int maxSize = device.GetMaxOutputReportLength();

        foreach (var command in commands)
        {
            var buffer = new byte[maxSize];
            buffer[0] = 0; // Report ID
            Array.Copy(command, 0, buffer, 1, Math.Min(command.Length, maxSize - 1));

            stream.Write(buffer);
        }
    }

    private HidDevice OpenKeyboardDevice()
    {
        var deviceList = DeviceList.Local;
        var devices = deviceList.GetHidDevices(OmenKeyboardConstants.VendorId, OmenKeyboardConstants.ProductId).ToList();

        _logger.LogDebug("Searching for HP Omen keyboard (VID: 0x{VendorId:X4}, PID: 0x{ProductId:X4})...",
            OmenKeyboardConstants.VendorId, OmenKeyboardConstants.ProductId);
        _logger.LogDebug("Found {Count} matching HID device(s)", devices.Count);

        if (!devices.Any())
        {
            throw new InvalidOperationException(
                $"HP Omen keyboard not found (VID: 0x{OmenKeyboardConstants.VendorId:X4}, PID: 0x{OmenKeyboardConstants.ProductId:X4}). " +
                "Please ensure the keyboard is connected and initialized.");
        }

        // The keyboard exposes multiple HID interfaces (keyboard input, media keys, LED control)
        // that share the same VID/PID. Select the one that supports output reports (LED control).
        var device = devices.FirstOrDefault(d => d.GetMaxOutputReportLength() > 0);
        if (device == null)
        {
            throw new InvalidOperationException(
                $"HP Omen keyboard LED interface not found (VID: 0x{OmenKeyboardConstants.VendorId:X4}, PID: 0x{OmenKeyboardConstants.ProductId:X4}). " +
                $"Found {devices.Count} device(s) but none support output reports.");
        }

        _logger.LogDebug("Selected keyboard LED interface: {DevicePath}", device.DevicePath);

        return device;
    }
}
#endif
