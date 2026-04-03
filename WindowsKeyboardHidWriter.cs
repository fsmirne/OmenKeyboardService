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

        foreach (var command in commands)
        {
            int maxSize = device.GetMaxOutputReportLength();
            if (maxSize <= 0)
                throw new InvalidOperationException("Device does not support output reports.");

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

        var device = devices.OrderByDescending(x => x.DevicePath).First();
        _logger.LogDebug("Selected keyboard device: {DevicePath}", device.DevicePath);

        return device;
    }
}
#endif
