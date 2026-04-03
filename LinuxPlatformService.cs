#if LINUX
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace OmenKeyboardService;

/// <summary>
/// Linux-specific platform service implementation
/// Handles device monitoring via /dev filesystem watching and suspend/resume detection
/// </summary>
public class LinuxPlatformService : PlatformServiceBase
{
    private FileSystemWatcher? _deviceWatcher;
    private FileSystemWatcher? _sleepWatcher;
    private CancellationTokenSource? _suspendMonitorCts;

    public override string PlatformName => "Linux";

    public LinuxPlatformService(ILogger<LinuxPlatformService> logger) : base(logger) { }

    public override async Task WaitForSystemReadyAsync(TimeSpan timeout, ILogger logger, CancellationToken cancellationToken)
    {
        int cpuCount = Environment.ProcessorCount;
        double loadThreshold = Math.Max(cpuCount * 0.7, 1.0);
        await SystemReadiness.WaitForLowLoadAsync(loadThreshold, timeout, logger, cancellationToken);
    }

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
    /// Sets up monitoring for system suspend/resume events
    /// Uses systemd's sleep.target or monitors /sys/power/state changes
    /// </summary>
    private void SetupSuspendResumeMonitoring()
    {
        try
        {
            // Try to monitor systemd sleep/wake events via systemd-sleep directory
            // When system resumes, systemd runs scripts in /lib/systemd/system-sleep/
            // We can monitor for resume by watching for specific file access patterns

            // Alternative: Monitor /sys/power/wakeup_count which changes on resume
            const string wakeupCountPath = "/sys/power/wakeup_count";
            if (File.Exists(wakeupCountPath))
            {
                _suspendMonitorCts = new CancellationTokenSource();
                _ = MonitorSuspendResumeAsync(wakeupCountPath, _suspendMonitorCts.Token);
                _logger.LogInformation("Suspend/resume monitoring enabled via wakeup_count");
            }
            else
            {
                // Fallback: Monitor /run/systemd/inhibit for lock file changes
                const string inhibitPath = "/run/systemd/inhibit";
                if (Directory.Exists(inhibitPath))
                {
                    _sleepWatcher = new FileSystemWatcher(inhibitPath)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                        EnableRaisingEvents = true
                    };
                    _sleepWatcher.Deleted += OnSleepInhibitorReleased;
                    _logger.LogInformation("Suspend/resume monitoring enabled via inhibitor directory");
                }
                else
                {
                    _logger.LogWarning("Could not set up suspend/resume monitoring. Resume detection may not work.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up suspend/resume monitoring");
        }
    }

    /// <summary>
    /// Monitors the wakeup_count file for changes indicating system resume
    /// </summary>
    private async Task MonitorSuspendResumeAsync(string wakeupCountPath, CancellationToken cancellationToken)
    {
        try
        {
            string lastValue = File.ReadAllText(wakeupCountPath).Trim();

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);

                try
                {
                    string currentValue = File.ReadAllText(wakeupCountPath).Trim();
                    if (currentValue != lastValue)
                    {
                        _logger.LogInformation("System resumed from suspend (wakeup_count changed: {Old} -> {New})", lastValue, currentValue);
                        lastValue = currentValue;

                        RequestColorReapply("System resumed from suspend", 2000, 5);
                    }
                }
                catch (IOException)
                {
                    // File may be temporarily unavailable during suspend
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
    /// Handles sleep inhibitor release events (system waking up)
    /// </summary>
    private void OnSleepInhibitorReleased(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogInformation("Sleep inhibitor released (possible resume): {Name}", e.Name);
            RequestColorReapply("Possible system resume detected", 2000, 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling sleep inhibitor event");
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
        _suspendMonitorCts?.Cancel();
        _suspendMonitorCts?.Dispose();
        _sleepWatcher?.Dispose();
        _deviceWatcher?.Dispose();
    }
}
#endif
