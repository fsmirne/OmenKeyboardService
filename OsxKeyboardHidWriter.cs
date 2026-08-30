#if OSX
using System.Runtime.Versioning;

namespace OmenKeyboardService;

/// <summary>
/// Writes HID commands to the keyboard via HidSharp (macOS HID API wrapper, backed by IOKit).
/// Shared read/write logic lives in <see cref="HidSharpKeyboardHidWriter"/>.
/// </summary>
[SupportedOSPlatform("macos")]
public class OsxKeyboardHidWriter : HidSharpKeyboardHidWriter
{
    public OsxKeyboardHidWriter(ILogger<OsxKeyboardHidWriter> logger) : base(logger) { }

    public override bool TryForceReenumeration()
    {
        // Programmatic USB re-enumeration on macOS requires IOKit (IOUSBDeviceInterface /
        // IOServiceRequestProbe) via P/Invoke, which HidSharp does not wrap. Not implemented —
        // unplug/replug the keyboard (or the hub it's on) if a re-enumeration is needed.
        _logger.LogWarning("Force re-enumeration is not implemented on macOS");
        return false;
    }
}
#endif
