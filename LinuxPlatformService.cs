#if LINUX
using Microsoft.Extensions.Logging;

namespace OmenKeyboardService;

/// <summary>
/// Linux-specific platform service implementation
/// Handles device monitoring via /dev filesystem watching and suspend/resume detection
/// </summary>
public class LinuxPlatformService : PlatformServiceBase
{
    private FileSystemWatcher? _deviceWatcher;
    private CancellationTokenSource? _suspendMonitorCts;

    public override string PlatformName => "Linux";

    public LinuxPlatformService(ILogger<LinuxPlatformService> logger) : base(logger) { }

    public override void Initialize()
    {
        _logger.LogInformation("Initializing Linux platform services...");

        // Set up device monitoring
        SetupDeviceMonitoring();

        // Set up suspend/resume monitoring
        SetupSuspendResumeMonitoring();

        _logger.LogInformation("Linux platform services initialized (device monitoring, suspend/resume detection)");
    }

    /// <summary>
    /// Sets up device monitoring to detect keyboard reconnection (e.g., KVM switch, USB replug)
    /// On Linux, we monitor /dev/hidraw* devices for changes
    /// </summary>
    private void SetupDeviceMonitoring()
    {
        try
        {
            // Monitor /dev for new hidraw device nodes
            if (Directory.Exists("/dev"))
            {
                _deviceWatcher = new FileSystemWatcher("/dev")
                {
                    Filter = "hidraw*",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                _deviceWatcher.Created += OnDeviceCreated;

                _logger.LogInformation("Device monitoring enabled. Will detect keyboard reconnection (KVM switch, USB replug, etc.)");
            }
            else
            {
                _logger.LogWarning("/dev directory not found. Device monitoring disabled.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up device monitoring. Keyboard reconnection detection will not work.");
        }
    }

    /// <summary>
    /// Sets up monitoring for system resume via wall-clock gap detection.
    /// </summary>
    private void SetupSuspendResumeMonitoring()
    {
        _suspendMonitorCts = new CancellationTokenSource();
        _ = MonitorSuspendResumeAsync(_suspendMonitorCts.Token);
        _logger.LogInformation("Suspend/resume monitoring enabled (wall-clock gap detection)");
    }

    /// <summary>
    /// Detects resume from suspend by watching for a wall-clock jump. The system clock
    /// does not advance while suspended, so if substantially more wall-clock time elapses
    /// than the poll interval, the machine was asleep in between.
    ///
    /// This replaces polling /sys/power/wakeup_count, which is NOT a resume counter: the
    /// kernel increments it on every wakeup event from any wakeup-capable device (USB,
    /// mouse, NIC, ...) during normal operation, so it changed constantly and triggered
    /// spurious color reapplications.
    /// </summary>
    private async Task MonitorSuspendResumeAsync(CancellationToken cancellationToken)
    {
        const int PollIntervalMs = 10000;
        // A suspend is inferred when elapsed wall-clock time exceeds the poll interval
        // by more than this slack, which absorbs scheduler jitter under load.
        const int SuspendThresholdMs = 5000;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var expectedWake = DateTime.UtcNow.AddMilliseconds(PollIntervalMs);
                await Task.Delay(PollIntervalMs, cancellationToken);

                var driftMs = (DateTime.UtcNow - expectedWake).TotalMilliseconds;
                if (driftMs > SuspendThresholdMs)
                {
                    _logger.LogInformation("System resumed from suspend (slept ~{Seconds}s)", (int)(driftMs / 1000));
                    RequestColorReapply("System resumed from suspend", 2000, 5);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when service stops
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in suspend/resume monitor");
        }
    }

    /// <summary>
    /// Handles device creation events and reapplies colors when the HP Omen keyboard is detected
    /// </summary>
    private void OnDeviceCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogDebug("New HID device detected: {DevicePath}", e.Name);

            // Check if this is our keyboard by examining the device info
            var devicePath = Path.Combine("/dev", e.Name ?? "");
            if (IsOmenKeyboard(devicePath))
            {
                _logger.LogInformation("HP Omen keyboard detected: {DevicePath}. Requesting color reapplication...", e.Name);
                RequestColorReapply("HP Omen keyboard reconnected", 1500, 5);
            }
            else
            {
                _logger.LogDebug("HID device {DevicePath} is not HP Omen keyboard, ignoring", e.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling device creation");
        }
    }

    /// <summary>
    /// Checks if a hidraw device is the HP Omen keyboard by reading its sysfs info
    /// </summary>
    private bool IsOmenKeyboard(string hidrawPath)
    {
        try
        {
            // Extract hidraw number (e.g., "hidraw0" -> "0")
            var deviceName = Path.GetFileName(hidrawPath);
            if (string.IsNullOrEmpty(deviceName) || !deviceName.StartsWith("hidraw"))
                return false;

            // Check sysfs for device info
            // Path: /sys/class/hidraw/hidrawX/device/uevent contains vendor/product info
            var sysfsPath = $"/sys/class/hidraw/{deviceName}/device/uevent";

            if (!File.Exists(sysfsPath))
            {
                // Sysfs not yet populated — skip this event. The keyboard will
                // fire another creation event once it is fully initialized, at
                // which point sysfs will be populated and we can identify it properly.
                _logger.LogDebug("Cannot read sysfs for {Device}, skipping (will retry on next device event)", deviceName);
                return false;
            }

            var uevent = File.ReadAllText(sysfsPath);

            // Look for HID_ID line which contains vendor:product
            // Format: HID_ID=0003:000003F0:00001F41
            var vendorHex = OmenKeyboardConstants.VendorId.ToString("X4");
            var productHex = OmenKeyboardConstants.ProductId.ToString("X4");

            bool isMatch = uevent.Contains(vendorHex, StringComparison.OrdinalIgnoreCase) && uevent.Contains(productHex, StringComparison.OrdinalIgnoreCase);

            return isMatch;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking if device is Omen keyboard, assuming yes to be safe");
            return true; // Assume it might be our keyboard if we can't check
        }
    }

    public override void Dispose()
    {
        try { _suspendMonitorCts?.Cancel(); } catch (ObjectDisposedException) { }
        _suspendMonitorCts?.Dispose();
        _deviceWatcher?.Dispose();
    }
}
#endif
