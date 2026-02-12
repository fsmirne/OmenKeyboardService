namespace OmenKeyboardService;

/// <summary>
/// Configuration model for the keyboard RGB settings
/// </summary>
public class KeyboardConfig
{
    public string? ProfileName { get; set; }

    public Dictionary<string, string> Profile { get; set; } = [];

    public string? LogLevel { get; set; }

    public string? Hotkey { get; set; }

    public int? RefreshIntervalSeconds { get; set; }

    public bool? KvmMode { get; set; }
}
