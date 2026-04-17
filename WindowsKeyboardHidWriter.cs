#if WINDOWS
using HidSharp;

namespace OmenKeyboardService;

/// <summary>
/// Writes HID commands to the keyboard via HidSharp (Windows HID API wrapper).
/// LED commands use Report ID 0, macro commands use Report ID 1.
/// </summary>
public class WindowsKeyboardHidWriter : IKeyboardHidWriter
{
    private readonly ILogger<WindowsKeyboardHidWriter> _logger;

    private static readonly byte[] VendorUsagePage = [0x06, 0x13, 0xFF];

    public WindowsKeyboardHidWriter(ILogger<WindowsKeyboardHidWriter> logger)
    {
        _logger = logger;
    }

    public void WriteCommands(byte[][] commands, int[]? delaysBeforeMs = null)
    {
        var device = FindKeyboardDevice();
        using var stream = device.Open();

        int maxSize = device.GetMaxOutputReportLength();

        for (int i = 0; i < commands.Length; i++)
        {
            int delay = delaysBeforeMs is not null && i < delaysBeforeMs.Length ? delaysBeforeMs[i] : 0;
            if (delay > 0)
                Thread.Sleep(delay);

            var buffer = new byte[maxSize];
            buffer[0] = 0; // Report ID 0 (LED)
            Array.Copy(commands[i], 0, buffer, 1, Math.Min(commands[i].Length, maxSize - 1));
            stream.Write(buffer);
        }
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
