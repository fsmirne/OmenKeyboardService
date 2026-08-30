#if OSX
using HidSharp;

namespace OmenKeyboardService;

/// <summary>
/// macOS-specific platform service implementation.
/// Handles device reconnect detection via HidSharp's device list and suspend/resume detection
/// via wall-clock gap monitoring (see <see cref="PlatformServiceBase.MonitorSuspendResumeAsync"/>).
/// </summary>
public class OsxPlatformService : PlatformServiceBase
{
    private CancellationTokenSource? _suspendMonitorCts;
    private bool _wasKeyboardPresent;

    public override string PlatformName => "macOS";

    public OsxPlatformService(ILogger<OsxPlatformService> logger) : base(logger) { }

    public override void Initialize()
    {
        _logger.LogInformation("Initializing macOS platform services...");

        SetupDeviceMonitoring();
        SetupSuspendResumeMonitoring();

        _logger.LogInformation("macOS platform services initialized (device monitoring, suspend/resume detection)");
    }

    /// <summary>
    /// Sets up device monitoring to detect keyboard reconnection (e.g., KVM switch, USB replug)
    /// via HidSharp's cross-process device list, which HidSharp backs with IOKit notifications on
    /// macOS. This avoids a direct IOKit/P-Invoke dependency.
    /// </summary>
    private void SetupDeviceMonitoring()
    {
        _wasKeyboardPresent = IsKeyboardPresent();
        DeviceList.Local.Changed += OnDeviceListChanged;
        _logger.LogInformation("Device monitoring enabled. Will detect keyboard reconnection (KVM switch, USB replug, etc.)");
    }

    private void SetupSuspendResumeMonitoring()
    {
        _suspendMonitorCts = new CancellationTokenSource();
        _ = MonitorSuspendResumeAsync(_suspendMonitorCts.Token);
        _logger.LogInformation("Suspend/resume monitoring enabled (wall-clock gap detection)");
    }

    /// <summary>
    /// Fires on any HID device list change system-wide; reapplies colors only on the
    /// absent-to-present transition of the HP Omen keyboard specifically.
    /// </summary>
    private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e)
    {
        try
        {
            bool isPresent = IsKeyboardPresent();

            if (isPresent && !_wasKeyboardPresent)
            {
                _logger.LogInformation("HP Omen keyboard detected. Requesting color reapplication...");
                RequestColorReapply("HP Omen keyboard reconnected", 1500, 5);
            }

            _wasKeyboardPresent = isPresent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling device list change");
        }
    }

    private static bool IsKeyboardPresent()
    {
        return DeviceList.Local.GetHidDevices(OmenKeyboardConstants.VendorId, OmenKeyboardConstants.ProductId).Any();
    }

    public override void Dispose()
    {
        DeviceList.Local.Changed -= OnDeviceListChanged;
        try { _suspendMonitorCts?.Cancel(); } catch (ObjectDisposedException) { }
        _suspendMonitorCts?.Dispose();
    }
}
#endif
