using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Management;
using System.Text.Json;

namespace OmenKeyboardService;

/// <summary>
/// Background service that monitors the configuration file and applies RGB settings
/// to the HP Omen keyboard when the service starts or when the config changes
/// </summary>
public class KeyboardRgbService : BackgroundService
{
    private readonly ILogger<KeyboardRgbService> _logger;
    private readonly OmenKeyboardController _keyboardController;
    private readonly string _configPath;
    private FileSystemWatcher? _configWatcher;
    private ManagementEventWatcher? _deviceArrivalWatcher;

    // HP Omen keyboard USB identifiers (matching OmenKeyboardController)
    private const int VENDOR_ID = 0x03F0;  // HP
    private const int PRODUCT_ID = 0x1F41; // Omen keyboard

    public KeyboardRgbService(
        ILogger<KeyboardRgbService> logger,
        OmenKeyboardController keyboardController)
    {
        _logger = logger;
        _keyboardController = keyboardController;

        // Config file should be in the same directory as the executable
        _configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HP Omen Keyboard RGB Service starting...");

        try
        {
            // Apply initial configuration
            await ApplyConfigurationAsync();

            // Set up file watcher to detect config changes
            SetupConfigWatcher();

            // Subscribe to power mode changes to handle sleep/wake events
            SetupPowerManagement();

            // Start monitoring for keyboard reconnection (e.g., KVM switch)
            SetupDeviceMonitoring();

            _logger.LogInformation("Service started successfully. Monitoring for config changes, power events, session changes, and USB device reconnection...");

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in service execution");
            throw;
        }
    }

    /// <summary>
    /// Reads and applies the color configuration from config.json
    /// </summary>
    /// <param name="retryCount">Number of retry attempts if keyboard is not ready</param>
    private async Task ApplyConfigurationAsync(int retryCount = 3)
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogWarning("Config file not found at {ConfigPath}. Creating default config.", _configPath);
                CreateDefaultConfig();
            }

            _logger.LogInformation("Reading configuration from {ConfigPath}", _configPath);

            // Read and parse the configuration file
            var json = await File.ReadAllTextAsync(_configPath);
            var config = JsonSerializer.Deserialize<KeyboardConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            if (config == null || config.Profile == null)
            {
                _logger.LogWarning("Invalid configuration. Using default white.");
                await ApplyColorsWithRetry(new Dictionary<string, uint>(), retryCount);
                return;
            }

            _logger.LogInformation("Applying profile: {ProfileName}", config.ProfileName ?? "Custom");

            // Convert hex color strings to uint values
            var colorMap = config.Profile
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => ParseHexColor(kvp.Value)
                );

            // Apply the colors to the keyboard with retry logic
            await ApplyColorsWithRetry(colorMap, retryCount);

            _logger.LogInformation("Successfully applied colors to keyboard. Groups configured: {Count}", colorMap.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply configuration");
        }
    }

    /// <summary>
    /// Applies colors with retry logic in case keyboard is not ready
    /// </summary>
    private async Task ApplyColorsWithRetry(Dictionary<string, uint> colorMap, int maxRetries)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < maxRetries)
        {
            try
            {
                attempt++;
                _keyboardController.ApplyColors(colorMap);

                // Success - log only if we had to retry
                if (attempt > 1)
                {
                    _logger.LogInformation("Successfully applied colors on attempt {Attempt}", attempt);
                }

                return; // Success, exit the retry loop
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Failed to apply colors (attempt {Attempt}/{MaxRetries}). Retrying in 1 second...",
                        attempt, maxRetries);
                    await Task.Delay(1000);
                }
            }
        }

        // All retries failed
        _logger.LogError(lastException, "Failed to apply colors after {MaxRetries} attempts. Keyboard may not be ready or connected.", maxRetries);
    }

    /// <summary>
    /// Parses a hex color string (e.g., "FF0000" or "#FF0000") to a uint RGB value
    /// </summary>
    private uint ParseHexColor(string hexColor)
    {
        // Remove '#' if present
        hexColor = hexColor.TrimStart('#');

        // Parse as hex
        if (uint.TryParse(hexColor, System.Globalization.NumberStyles.HexNumber, null, out uint color))
        {
            return color;
        }

        _logger.LogWarning("Invalid hex color format: {HexColor}. Using white.", hexColor);
        return 0xFFFFFF; // Default to white
    }

    /// <summary>
    /// Sets up a file watcher to automatically reload config when it changes
    /// </summary>
    private void SetupConfigWatcher()
    {
        try
        {
            var configDirectory = Path.GetDirectoryName(_configPath);
            if (string.IsNullOrEmpty(configDirectory))
                return;

            _configWatcher = new FileSystemWatcher(configDirectory)
            {
                Filter = "config.json",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _configWatcher.Changed += async (sender, e) =>
            {
                _logger.LogInformation("Configuration file changed. Reloading...");

                // Small delay to ensure file is fully written
                await Task.Delay(500);

                await ApplyConfigurationAsync();
            };

            _logger.LogInformation("File watcher enabled for config.json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up config file watcher. Changes will not be auto-detected.");
        }
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
    private async void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Power mode changed: {Mode}", e.Mode);

            // Reapply colors when resuming from sleep or suspend
            if (e.Mode == PowerModes.Resume)
            {
                _logger.LogInformation("System resumed from sleep. Waiting for hardware to initialize...");

                // Longer delay to allow hardware to fully initialize after wake
                // Some systems need more time for USB devices to be ready
                await Task.Delay(2000);

                _logger.LogInformation("Attempting to reapply keyboard colors after resume...");
                await ApplyConfigurationAsync(retryCount: 5);
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
    private async void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        try
        {
            _logger.LogInformation("Session event: {Reason}", e.Reason);

            // Reapply colors when unlocking the session
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                _logger.LogInformation("Session unlocked. Waiting for keyboard to be ready...");

                // Delay to allow keyboard to wake from low-power state
                await Task.Delay(1000);

                _logger.LogInformation("Attempting to reapply keyboard colors after unlock...");
                await ApplyConfigurationAsync(retryCount: 5);
            }
            // Also handle remote desktop connections
            else if (e.Reason == SessionSwitchReason.ConsoleConnect ||
                     e.Reason == SessionSwitchReason.RemoteConnect)
            {
                _logger.LogInformation("Console/Remote connected. Reapplying keyboard colors...");

                await Task.Delay(500);

                await ApplyConfigurationAsync(retryCount: 3);
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

            _logger.LogInformation("USB device monitoring enabled. Will detect keyboard reconnection instantly (KVM switch, USB replug, etc.)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up USB device monitoring. Keyboard reconnection detection will not work.");
        }
    }

    /// <summary>
    /// Handles USB device arrival events and reapplies colors when the keyboard reconnects
    /// </summary>
    private async void OnDeviceArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            _logger.LogInformation("HP Omen keyboard reconnected (KVM switch or USB replug detected). Waiting for device initialization...");

            // Delay to allow device to fully initialize
            await Task.Delay(1000);

            _logger.LogInformation("Attempting to apply colors to reconnected keyboard...");
            await ApplyConfigurationAsync(retryCount: 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling device arrival");
        }
    }

    /// <summary>
    /// Creates a default configuration file with example profiles
    /// </summary>
    private void CreateDefaultConfig()
    {
        var defaultConfig = new KeyboardConfig
        {
            ProfileName = "Gaming",
            Profile = new Dictionary<string, string>
            {
                ["fps"] = "FF0000",        // WASD keys - Red
                ["arrows"] = "00FF00",     // Arrow keys - Green
                ["fkeys"] = "0000FF",      // Function keys - Blue
                ["pkeys"] = "FF00FF",      // P1-P5 keys - Magenta
                ["media"] = "FFFF00",      // Media keys - Yellow
                ["numpad"] = "00FFFF",     // Numpad - Cyan
                ["windows"] = "FF6600"     // Windows key - Orange
            }
        };

        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_configPath, json);
        _logger.LogInformation("Created default configuration file");
    }

    public override void Dispose()
    {
        _configWatcher?.Dispose();

        if (_deviceArrivalWatcher != null)
        {
            _deviceArrivalWatcher.Stop();
            _deviceArrivalWatcher.Dispose();
        }

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        base.Dispose();
    }
}

/// <summary>
/// Configuration model for the keyboard RGB settings
/// </summary>
public class KeyboardConfig
{
    /// <summary>
    /// Name of the profile (for documentation purposes)
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// Dictionary mapping key group names to hex color values
    /// Valid groups: fps, arrows, fkeys, pkeys, media, numpad, system, windows
    /// Individual keys can also be specified by name
    /// Color format: "RRGGBB" (e.g., "FF0000" for red)
    /// </summary>
    public Dictionary<string, string> Profile { get; set; } = new();
}
