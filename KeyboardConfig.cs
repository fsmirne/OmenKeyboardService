using Microsoft.Extensions.Logging;

namespace OmenKeyboardService;

/// <summary>
/// Configuration model for keyboard RGB and macro key settings
/// </summary>
public class KeyboardConfig
{
    public string? ProfileName { get; set; }

    public Dictionary<string, string> Profile { get; set; } = [];

    public string? LogLevel { get; set; }

    public int? RefreshIntervalSeconds { get; set; }

    public bool? KvmMode { get; set; }

    /// <summary>
    /// Macro key assignments. Keys are "p1"-"p6" or "fn+p1"-"fn+p6".
    /// Values define what the key does when pressed.
    /// </summary>
    public Dictionary<string, MacroDefinition>? Macros { get; set; }

    public void Validate()
    {
        if (RefreshIntervalSeconds.HasValue && RefreshIntervalSeconds.Value < 0)
            throw new InvalidOperationException($"RefreshIntervalSeconds must be >= 0, got {RefreshIntervalSeconds.Value}");

        if (LogLevel != null && !Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(LogLevel, true, out _))
            throw new InvalidOperationException($"Invalid LogLevel '{LogLevel}'. Valid values: Trace, Debug, Information, Warning, Error, Critical, None");

        if (Profile == null)
            throw new InvalidOperationException("Profile must not be null");

        if (Macros != null)
            ValidateMacros(Macros);
    }

    private static void ValidateMacros(Dictionary<string, MacroDefinition> macros)
    {
        foreach (var (keyName, macro) in macros)
        {
            if (!OmenMacroController.MacroKeySlots.ContainsKey(keyName.ToLowerInvariant()))
                throw new InvalidOperationException($"Invalid macro key '{keyName}'. Valid keys: {string.Join(", ", OmenMacroController.MacroKeySlots.Keys)}");

            if (macro.Keys != null && macro.Sequence != null)
                throw new InvalidOperationException($"Macro key '{keyName}': specify either 'keys' or 'sequence', not both.");

            if (macro.Keys == null && macro.Sequence == null)
                throw new InvalidOperationException($"Macro key '{keyName}': must specify 'keys' or 'sequence'.");

            if (macro.Keys != null)
                HidKeyCodes.ParseKeyCombo(macro.Keys); // throws on invalid key names

            if (macro.Sequence != null)
            {
                if (macro.Sequence.Count == 0)
                    throw new InvalidOperationException($"Macro key '{keyName}': 'sequence' must not be empty.");

                foreach (var step in macro.Sequence)
                {
                    HidKeyCodes.ParseKeyCombo(step.Keys); // throws on invalid key names
                    if (step.DelayMs < 0 || step.DelayMs > 65535)
                        throw new InvalidOperationException($"Macro key '{keyName}': delayMs must be 0-65535, got {step.DelayMs}.");
                }
            }
        }
    }
}

/// <summary>
/// Defines what a macro key does when pressed.
/// Use "keys" for a single key/combo, or "sequence" for multi-step macros.
/// </summary>
public class MacroDefinition
{
    /// <summary>Single key or combo: "a", "ctrl+c", "ctrl+shift+t"</summary>
    public string? Keys { get; set; }

    /// <summary>Multi-step sequence with optional delays between steps</summary>
    public List<MacroStep>? Sequence { get; set; }
}

/// <summary>
/// A single step in a macro sequence
/// </summary>
public class MacroStep
{
    /// <summary>Key combo for this step: "ctrl+c", "a", etc.</summary>
    public required string Keys { get; set; }

    /// <summary>Delay in milliseconds after this step (before the next). Default 0.</summary>
    public int DelayMs { get; set; }
}
