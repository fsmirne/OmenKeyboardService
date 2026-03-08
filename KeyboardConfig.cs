using Microsoft.Extensions.Logging;

namespace OmenKeyboardService;

/// <summary>
/// Configuration model for the keyboard RGB settings
/// </summary>
public class KeyboardConfig
{
    public string? ProfileName { get; set; }

    public Dictionary<string, string> Profile { get; set; } = [];

    public string? LogLevel { get; set; }

    public int? RefreshIntervalSeconds { get; set; }

    public bool? KvmMode { get; set; }

    /// <summary>
    /// Seconds to wait after service start before applying colors.
    /// Useful when the service starts at boot before the keyboard is ready.
    /// </summary>
    public int? StartupDelaySeconds { get; set; }

    /// <summary>
    /// Validates configuration values and throws if any are invalid
    /// </summary>
    public void Validate()
    {
        if (RefreshIntervalSeconds.HasValue && RefreshIntervalSeconds.Value < 0)
        {
            throw new InvalidOperationException($"RefreshIntervalSeconds must be >= 0, got {RefreshIntervalSeconds.Value}");
        }

        if (StartupDelaySeconds.HasValue && StartupDelaySeconds.Value < 0)
        {
            throw new InvalidOperationException($"StartupDelaySeconds must be >= 0, got {StartupDelaySeconds.Value}");
        }

        if (LogLevel != null && !Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(LogLevel, true, out _))
        {
            throw new InvalidOperationException($"Invalid LogLevel '{LogLevel}'. Valid values: Trace, Debug, Information, Warning, Error, Critical, None");
        }

        if (Profile == null)
        {
            throw new InvalidOperationException("Profile must not be null");
        }
    }
}
