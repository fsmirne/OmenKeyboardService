using System.Threading.Channels;

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
    private readonly KeyboardConfigProvider _configProvider;
    private readonly string _configPath;
    private FileSystemWatcher? _configWatcher;
    private KeyboardConfig? _currentConfig;

    // Debouncing for config file changes
    private CancellationTokenSource? _configChangeCts;
    private readonly object _configChangeLock = new object();

    private PeriodicTimer? _periodicTimer;
    

    private readonly Channel<ColorReapplyEventArgs> _reapplyChannel = Channel.CreateUnbounded<ColorReapplyEventArgs>();
    private Task? _reapplyWorkerTask;

    private CancellationTokenSource? _serviceCts;

    public KeyboardRgbService(
        ILogger<KeyboardRgbService> logger,
        OmenKeyboardController keyboardController,
        IPlatformService platformService,
        KeyboardConfigProvider configProvider)
    {
        _logger = logger;
        _keyboardController = keyboardController;
        _platformService = platformService;
        _configProvider = configProvider;

        // Config file should be in the same directory as the executable
        _configPath = _configProvider.ConfigPath;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _serviceCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        _logger.LogInformation("HP Omen Keyboard RGB Service starting on {Platform}...", _platformService.PlatformName);

        try
        {
            // Apply initial configuration
            await ApplyConfigurationAsync();

            // Set up file watcher to detect config changes
            SetupConfigWatcher();

            // Initialize platform-specific services (power events, device monitoring, etc.)
            _platformService.ColorReapplyRequested += OnColorReapplyRequested;
            _platformService.Initialize(_currentConfig?.Hotkey);

            // Set up periodic refresh if configured
            SetupPeriodicRefresh();

            _reapplyWorkerTask = RunReapplyWorkerAsync(stoppingToken);

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
            _logger.LogInformation("Reading configuration from {ConfigPath}", _configPath);

            // Read and parse the configuration file
            var config = await _configProvider.LoadOrCreateDefaultAsync();

            if (config == null || config.Profile == null)
            {
                _logger.LogWarning("Invalid configuration. Using default white.");
                await ApplyColorsWithRetry(new Dictionary<string, uint>(), retryCount);
                return;
            }

            // Store current config for later reference
            _currentConfig = config;

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
    /// Applies colors with retry logic using exponential backoff in case keyboard is not ready
    /// </summary>
    private async Task ApplyColorsWithRetry(Dictionary<string, uint> colorMap, int maxRetries)
    {
        int attempt = 0;
        Exception? lastException = null;

        // In KVM mode, use longer initial delay and higher cap for retry intervals
        bool kvmMode = _currentConfig?.KvmMode ?? false;
        int delayMs = kvmMode ? 1000 : 500; // KVM: start with 1s, normal: 500ms
        int maxDelayMs = kvmMode ? 30000 : 8000; // KVM: cap at 30s, normal: 8s

        while (attempt < maxRetries)
        {
            try
            {
                attempt++;
                _keyboardController.ApplyColors(colorMap);

                // Success - log only if we had to retry
                if (attempt > 1)
                {
                    _logger.LogInformation("Successfully applied colors on attempt {Attempt}{KvmNote}",
                        attempt, kvmMode ? " (KVM mode)" : "");
                }

                return; // Success, exit the retry loop
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxRetries)
                {
                    string kvmNote = kvmMode ? " (KVM mode - keyboard may be switched to another computer)" : "";
                    _logger.LogWarning(ex, "Failed to apply colors (attempt {Attempt}/{MaxRetries}). Retrying in {DelayMs}ms...{KvmNote}",
                        attempt, maxRetries, delayMs, kvmNote);
                    await Task.Delay(delayMs);

                    // Exponential backoff with cap
                    delayMs = Math.Min(delayMs * 2, maxDelayMs);
                }
            }
        }

        // All retries failed
        if (kvmMode)
        {
            _logger.LogWarning(lastException, "Failed to apply colors after {MaxRetries} attempts (KVM mode). " +
                "Keyboard may be switched to another computer. Will retry when keyboard reconnects or on next trigger.", maxRetries);
        }
        else
        {
            _logger.LogError(lastException, "Failed to apply colors after {MaxRetries} attempts. Keyboard may not be ready or connected.", maxRetries);
        }
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
    /// Uses debouncing to handle multiple rapid file system events
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

            _configWatcher.Changed += OnConfigFileChanged;

            _logger.LogInformation("File watcher enabled for config.json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up config file watcher. Changes will not be auto-detected.");
        }
    }

    /// <summary>
    /// Handles config file change events with debouncing
    /// FileSystemWatcher often fires multiple events for a single change
    /// </summary>
    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_configChangeLock)
        {
            // Cancel any pending config reload
            _configChangeCts?.Cancel();
            _configChangeCts?.Dispose();
            _configChangeCts = new CancellationTokenSource();

            var token = _configChangeCts.Token;

            // Debounce: wait 500ms before actually reloading
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, token);

                    if (!token.IsCancellationRequested)
                    {
                        _logger.LogInformation("Configuration file changed. Reloading...");
                        await ApplyConfigurationAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Another change came in, this reload was cancelled
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reloading configuration after file change");
                }
            }, token);
        }
    }


    /// <summary>
    /// Sets up periodic color refresh if configured
    /// Useful for KVM setups where display power events don't fire reliably
    /// </summary>
    private void SetupPeriodicRefresh()
    {
        if (_currentConfig?.RefreshIntervalSeconds > 0)
        {
            var intervalMs = _currentConfig.RefreshIntervalSeconds.Value * 1000;
            _logger.LogInformation("Periodic refresh enabled: colors will be reapplied every {Seconds} seconds", _currentConfig.RefreshIntervalSeconds.Value);

            _periodicTimer?.Dispose();
            _periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

            _ = Task.Run(async () =>
            {
                try
                {
                    if (_serviceCts == null)
                        return;

                    while (await _periodicTimer.WaitForNextTickAsync(_serviceCts.Token))
                    {
                        try
                        {
                            _logger.LogDebug("Periodic refresh triggered");
                            await ApplyConfigurationAsync(retryCount: 3);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error during periodic refresh");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });
        }
    }

    /// <summary>
    /// Handles color reapply requests from the platform service
    /// </summary>
    private void OnColorReapplyRequested(object? sender, ColorReapplyEventArgs e)
    {
        _reapplyChannel.Writer.TryWrite(e);
    }

    private async Task RunReapplyWorkerAsync(CancellationToken stoppingToken)
    {
        await foreach (var e in _reapplyChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("{Reason}. Waiting {DelayMs}ms for device initialization...", e.Reason, e.DelayMs);
                await Task.Delay(e.DelayMs, stoppingToken);

                _logger.LogInformation("Attempting to reapply keyboard colors...");

                bool kvmMode = _currentConfig?.KvmMode ?? false;
                int retryCount = kvmMode ? Math.Max(e.RetryCount, 10) : e.RetryCount;

                try
                {
                    await ApplyConfigurationAsync(retryCount: retryCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reapply colors. Device may not be ready or connected.");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling color reapply request");
            }
        }
    }

    public override void Dispose()
    {
        _serviceCts?.Cancel();
        _serviceCts?.Dispose();
        _configChangeCts?.Cancel();
        _configChangeCts?.Dispose();
        _configWatcher?.Dispose();
        _periodicTimer?.Dispose();
        _platformService.ColorReapplyRequested -= OnColorReapplyRequested;
        _platformService.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
