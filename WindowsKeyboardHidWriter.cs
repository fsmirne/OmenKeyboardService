#if WINDOWS
using System.Runtime.Versioning;

namespace OmenKeyboardService;

/// <summary>
/// Writes HID commands to the keyboard via HidSharp (Windows HID API wrapper).
/// Shared read/write logic lives in <see cref="HidSharpKeyboardHidWriter"/>; this class adds
/// Windows-specific USB re-enumeration via SetupAPI.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsKeyboardHidWriter : HidSharpKeyboardHidWriter
{
    public WindowsKeyboardHidWriter(ILogger<WindowsKeyboardHidWriter> logger) : base(logger) { }

    public override bool TryForceReenumeration()
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
}
#endif
