#if LINUX
using Microsoft.Extensions.Logging;

namespace OmenKeyboardService;

/// <summary>
/// Linux-specific platform service implementation
/// Handles device monitoring via /dev filesystem watching
/// </summary>
public class LinuxPlatformService : IPlatformService
{
    private readonly ILogger<LinuxPlatformService> _logger;
    private FileSystemWatcher? _deviceWatcher;

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
            _logger.LogInformation("New HID device detected: {DevicePath}. Triggering color reapply...", e.Name);

            ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
            {
                Reason = "New HID device detected (potentially reconnected keyboard)",
                DelayMs = 1500,
                RetryCount = 3
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling device creation");
        }
    }

    public void Dispose()
    {
        _deviceWatcher?.Dispose();
    }
}
#endif
