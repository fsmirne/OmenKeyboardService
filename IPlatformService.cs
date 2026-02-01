namespace OmenKeyboardService;

/// <summary>
/// Interface for platform-specific services (Windows or Linux)
/// Abstracts power management, session monitoring, and device detection
/// </summary>
public interface IPlatformService : IDisposable
{
    /// <summary>
    /// Event raised when colors should be reapplied (power resume, session unlock, device reconnect, etc.)
    /// </summary>
    event EventHandler<ColorReapplyEventArgs>? ColorReapplyRequested;

    /// <summary>
    /// Initializes platform-specific monitoring (power events, device events, etc.)
    /// </summary>
    /// <param name="hotkey">Optional global hotkey to manually trigger color reapplication</param>
    void Initialize(string? hotkey = null);

    /// <summary>
    /// Gets the platform name for logging purposes
    /// </summary>
    string PlatformName { get; }
}

/// <summary>
/// Event arguments for color reapply requests
/// </summary>
public class ColorReapplyEventArgs : EventArgs
{
    /// <summary>
    /// Reason for reapplying colors
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Delay in milliseconds before attempting to reapply colors (for device initialization)
    /// </summary>
    public int DelayMs { get; init; } = 1000;

    /// <summary>
    /// Number of retry attempts if the operation fails
    /// </summary>
    public int RetryCount { get; init; } = 5;
}
