#if LINUX
using Microsoft.Extensions.Logging;
using HidSharp;

namespace OmenKeyboardService;

/// <summary>
/// Linux-specific platform service implementation
/// Handles device monitoring via /dev filesystem watching
/// </summary>
public class LinuxPlatformService : IPlatformService
{
    private readonly ILogger<LinuxPlatformService> _logger;
    private FileSystemWatcher? _deviceWatcher;
    private bool _bootupPeriod = true;

    // HP Omen keyboard USB identifiers
    private const int VENDOR_ID = 0x03F0;  // HP
    private const int PRODUCT_ID = 0x1F41; // Omen keyboard

    public string PlatformName => "Linux";

    public event EventHandler<ColorReapplyEventArgs>? ColorReapplyRequested;

    public LinuxPlatformService(ILogger<LinuxPlatformService> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        _logger.LogInformation("Initializing Linux platform services...");

        // Set up device monitoring
        SetupDeviceMonitoring();

        _logger.LogInformation("Linux platform services initialized (device monitoring via /dev)");
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

                // After 10 seconds, exit bootup period for faster response to device events
                Task.Delay(10000).ContinueWith(_ =>
                {
                    _bootupPeriod = false;
                    _logger.LogInformation("Boot period complete, device monitoring now fully active");
                });
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
    /// Handles device creation events and reapplies colors when a new HID device is detected
    /// </summary>
    private void OnDeviceCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogDebug("New HID device detected: {DevicePath}", e.Name);

            // During boot, be more cautious and verify before triggering
            if (_bootupPeriod)
            {
                _logger.LogDebug("Boot period active - verifying device before triggering reapply");
                _ = VerifyAndTriggerReapplyAsync(e.Name);
            }
            else
            {
                // After boot, trigger immediately (lower latency for KVM switches, etc.)
                _logger.LogInformation("New HID device detected: {DevicePath}. Triggering color reapply...", e.Name);
                ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
                {
                    Reason = "New HID device detected (potentially reconnected keyboard)",
                    DelayMs = 1500,
                    RetryCount = 3
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling device creation");
        }
    }

    /// <summary>
    /// Verifies if the newly detected device is the HP Omen keyboard before triggering a color reapply
    /// Uses retry logic with exponential backoff to handle boot-time race conditions
    /// </summary>
    private async Task VerifyAndTriggerReapplyAsync(string deviceName)
    {
        const int maxRetries = 5;
        const int initialDelayMs = 100;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Exponential backoff: 100ms, 200ms, 400ms, 800ms, 1600ms
                if (attempt > 0)
                {
                    int delayMs = initialDelayMs * (1 << (attempt - 1));
                    _logger.LogDebug("Retry attempt {Attempt}/{MaxRetries} after {Delay}ms delay",
                        attempt + 1, maxRetries, delayMs);
                    await Task.Delay(delayMs);
                }

                // Check if HP Omen keyboard is present
                if (IsOmenKeyboardPresent())
                {
                    _logger.LogInformation("HP Omen keyboard verified (attempt {Attempt}). Triggering color reapply...",
                        attempt + 1);

                    ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
                    {
                        Reason = $"HP Omen keyboard reconnected (verified after {attempt + 1} attempts)",
                        DelayMs = 1500,
                        RetryCount = 3
                    });

                    return; // Success!
                }
                else
                {
                    // Not our keyboard, exit early
                    _logger.LogDebug("Device {DeviceName} is not HP Omen keyboard", deviceName);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to verify device on attempt {Attempt}/{MaxRetries}",
                    attempt + 1, maxRetries);

                // Continue to next retry
            }
        }

        _logger.LogDebug("Failed to verify device {DeviceName} after {MaxRetries} attempts",
            deviceName, maxRetries);
    }

    /// <summary>
    /// Checks if the HP Omen keyboard is currently present using HidSharp
    /// </summary>
    private bool IsOmenKeyboardPresent()
    {
        try
        {
            var deviceList = DeviceList.Local;
            var devices = deviceList.GetHidDevices(VENDOR_ID, PRODUCT_ID);

            bool found = devices.Any();
            _logger.LogDebug("HP Omen keyboard present: {Found}", found);
            return found;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check for HP Omen keyboard");
            return false;
        }
    }

    public void Dispose()
    {
        _deviceWatcher?.Dispose();
    }
}
#endif
