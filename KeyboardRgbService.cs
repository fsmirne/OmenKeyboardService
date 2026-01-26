using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly IPlatformService _platformService;
    private readonly string _configPath;
    private FileSystemWatcher? _configWatcher;

    public KeyboardRgbService(
        ILogger<KeyboardRgbService> logger,
        OmenKeyboardController keyboardController,
        IPlatformService platformService)
    {
        _logger = logger;
        _keyboardController = keyboardController;
        _platformService = platformService;

        // Config file should be in the same directory as the executable
        _configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HP Omen Keyboard RGB Service starting on {Platform}...", _platformService.PlatformName);

        try
        {
            // Apply initial configuration
            await ApplyConfigurationAsync();

            // Set up file watcher to detect config changes
            SetupConfigWatcher();

            // Initialize platform-specific services (power events, device monitoring, etc.)
            _platformService.ColorReapplyRequested += OnColorReapplyRequested;
            _platformService.Initialize();

            _logger.LogInformation("Service started successfully. Monitoring for config changes and platform events...");

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
    /// Handles color reapply requests from the platform service
    /// </summary>
    private async void OnColorReapplyRequested(object? sender, ColorReapplyEventArgs e)
    {
        try
        {
            _logger.LogInformation("{Reason}. Waiting {DelayMs}ms for device initialization...", e.Reason, e.DelayMs);

            // Delay to allow device to fully initialize
            await Task.Delay(e.DelayMs);

            _logger.LogInformation("Attempting to reapply keyboard colors...");

            // Try to apply configuration - if keyboard is not present, this will fail gracefully
            try
            {
                await ApplyConfigurationAsync(retryCount: e.RetryCount);
            }
            catch (Exception ex)
            {
                // Log but don't crash - device might not be ready or not our keyboard
                _logger.LogWarning(ex, "Failed to reapply colors. Device may not be ready or connected.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling color reapply request");
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
            LogLevel = "Warning",
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
        _platformService.ColorReapplyRequested -= OnColorReapplyRequested;
        _platformService.Dispose();
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

    /// <summary>
    /// Minimum log level for the service
    /// Valid values: "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None"
    /// Default: "Warning" (only warnings and errors will be logged)
    /// Set to "Information" to enable detailed logging for debugging
    /// </summary>
    public string? LogLevel { get; set; }
}
