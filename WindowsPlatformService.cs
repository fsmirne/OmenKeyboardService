#if WINDOWS
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Management;
using System.Runtime.Versioning;

namespace OmenKeyboardService;

/// <summary>
/// Windows-specific platform service implementation
/// Handles Windows power events, session events, and WMI device monitoring
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsPlatformService : IPlatformService
{
    private readonly ILogger<WindowsPlatformService> _logger;
    private ManagementEventWatcher? _deviceArrivalWatcher;

    // HP Omen keyboard USB identifiers
    private const int VENDOR_ID = 0x03F0;
    private const int PRODUCT_ID = 0x1F41;

    public string PlatformName => "Windows";

    public event EventHandler<ColorReapplyEventArgs>? ColorReapplyRequested;

    public WindowsPlatformService(ILogger<WindowsPlatformService> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        _logger.LogInformation("Initializing Windows platform services...");

        // Set up power management monitoring
        SetupPowerManagement();

        // Set up device monitoring
        SetupDeviceMonitoring();

        _logger.LogInformation("Windows platform services initialized (power events, session events, WMI device monitoring)");
    }

    /// <summary>
    /// Sets up power management event monitoring to restore colors after sleep/wake
    /// </summary>
    private void SetupPowerManagement()
    {
        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            _logger.LogInformation("Power management and session monitoring enabled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up power management monitoring");
        }
    }

    /// <summary>
    /// Handles power mode changes (sleep, resume, battery status)
    /// </summary>
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Power mode changed: {Mode}", e.Mode);

            // Reapply colors when resuming from sleep or suspend
            if (e.Mode == PowerModes.Resume)
            {
                _logger.LogInformation("System resumed from sleep. Requesting color reapplication...");

                ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
                {
                    Reason = "System resumed from sleep",
                    DelayMs = 2000,  // Longer delay for hardware initialization
                    RetryCount = 5
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling power mode change");
        }
    }

    /// <summary>
    /// Handles session switch events (lock, unlock, remote connect/disconnect)
    /// </summary>
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        try
        {
            _logger.LogInformation("Session event: {Reason}", e.Reason);

            // Reapply colors when unlocking the session
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                _logger.LogInformation("Session unlocked. Requesting color reapplication...");

                ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
                {
                    Reason = "Session unlocked",
                    DelayMs = 1000,
                    RetryCount = 5
                });
            }
            // Also handle remote desktop connections
            else if (e.Reason == SessionSwitchReason.ConsoleConnect ||
                     e.Reason == SessionSwitchReason.RemoteConnect)
            {
                _logger.LogInformation("Console/Remote connected. Requesting color reapplication...");

                ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
                {
                    Reason = "Console/Remote connected",
                    DelayMs = 500,
                    RetryCount = 3
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session switch");
        }
    }

    /// <summary>
    /// Sets up WMI event monitoring to detect keyboard reconnection (e.g., KVM switch, USB replug)
    /// </summary>
    private void SetupDeviceMonitoring()
    {
        try
        {
            // WMI query to watch for USB device arrival events
            // This fires immediately when any USB device is connected
            var query = new WqlEventQuery(
                "SELECT * FROM __InstanceCreationEvent WITHIN 1 " +
                "WHERE TargetInstance ISA 'Win32_PnPEntity' " +
                "AND TargetInstance.DeviceID LIKE 'HID\\\\VID_03F0&PID_1F41%'");

            _deviceArrivalWatcher = new ManagementEventWatcher(query);
            _deviceArrivalWatcher.EventArrived += OnDeviceArrived;
            _deviceArrivalWatcher.Start();

            _logger.LogInformation("WMI device monitoring enabled. Will detect keyboard reconnection (KVM switch, USB replug, etc.)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up WMI device monitoring. Keyboard reconnection detection will not work.");
        }
    }

    /// <summary>
    /// Handles USB device arrival events and reapplies colors when the keyboard reconnects
    /// </summary>
    private void OnDeviceArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            _logger.LogInformation("HP Omen keyboard reconnected (KVM switch or USB replug detected)");

            ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
            {
                Reason = "HP Omen keyboard reconnected",
                DelayMs = 1000,
                RetryCount = 5
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling device arrival");
        }
    }

    public void Dispose()
    {
        if (_deviceArrivalWatcher != null)
        {
            _deviceArrivalWatcher.Stop();
            _deviceArrivalWatcher.Dispose();
        }

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
#endif
