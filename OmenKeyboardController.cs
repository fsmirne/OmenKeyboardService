namespace OmenKeyboardService;

/// <summary>
/// Controller for HP Omen keyboard RGB LED control
/// Builds HID command packets and delegates device I/O to IKeyboardHidWriter
/// </summary>
public class OmenKeyboardController
{
    private readonly ILogger<OmenKeyboardController> _logger;
    private readonly IKeyboardHidWriter _hidWriter;

    // HID protocol headers - magic bytes that the keyboard firmware expects.
    // The full apply sequence mirrors HP's McuKBLightingHSAClient.SetKeyColor(..., enabelUersMode: true):
    //   LIGHTING_ON -> (50ms) -> USER_MODE_ON -> (50ms) -> KEY_UNLOCK -> (50ms) -> 9 color pages -> (50ms) -> KEY_LOCK
    // Without LIGHTING_ON the MCU silently drops color writes after a sleep/wake cycle (firmware holds LED power off for standby).

    // SetKeyBoardLightingOn: Command=9, BLength_low=1, InfoBytes[0]=0xFF — turns the LED controller on.
    private const string HEADER_LIGHTING_ON = "09000100ff0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

    // SetUserModeEnable: Command=4, BLength_low=2, InfoBytes=0xFC,0xEA — switches the MCU to user (per-key) mode.
    private const string HEADER0 = "04000200fcea00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

    // SetAllKeyCanBeChange(true): Command=4, Index=1, BLength_low=18, all zeros — unlocks keys for color writes.
    private const string HEADER_KEY_UNLOCK = "04011200000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

    // SetAllKeyCanBeChange(false): Command=4, Index=1, BLength_low=18, 18 bytes of 0xFF — commits/locks the written colors.
    private const string HEADER_KEY_LOCK = "04011200ffffffffffffffffffffffffffffffffffff000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

    // HEADER1-9 are command headers for the color data packets
    // Each set of 3 headers (1-3, 4-6, 7-9) contains R, G, B values at different bit offsets (16, 8, 0)
    private const string HEADER1 = "05003c00";  // First batch, Red component (offset 16)
    private const string HEADER2 = "05013c00";  // First batch, Green component (offset 8)
    private const string HEADER3 = "05021800";  // First batch, Blue component (offset 0)
    private const string HEADER4 = "06003c00";  // Second batch, Red component
    private const string HEADER5 = "06013c00";  // Second batch, Green component
    private const string HEADER6 = "06021800";  // Second batch, Blue component
    private const string HEADER7 = "07003c00";  // Third batch, Red component
    private const string HEADER8 = "07013c00";  // Third batch, Green component
    private const string HEADER9 = "07021800";  // Third batch, Blue component

    private const int PhaseDelayMs = 50;

    // Body templates define which keys are controlled in each packet
    // 'ff' = active key slot (will be replaced with color data)
    // '00' = unused/padding slot
    private const string BODY0 = "ffffffffffffffffffffffffffff00ffffffffff00ffff00ffffffffff00ffffffffffffffffffffffffffffff0000ffffffffffff00ffff00ffff00";
    private const string BODY1 = "ffff0000ffffffffffffffff00ffff00ffff0000ffffffffff00ffffffffff00ffff0000ffffffffff00ffffff00ff00ffff0000ffffffffffffffff";
    private const string BODY2 = "ffffff00ffff0000ffffffffffffffffffff0000ffff0000000000000000000000000000000000000000000000000000000000000000000000000000";

    public OmenKeyboardController(ILogger<OmenKeyboardController> logger, IKeyboardHidWriter hidWriter)
    {
        _logger = logger;
        _hidWriter = hidWriter;
    }

    /// <summary>
    /// Applies color settings to the keyboard based on key group mappings
    /// </summary>
    public void ApplyColors(Dictionary<string, uint> colorOverrides)
    {
        try
        {
            _logger.LogInformation("Applying colors to keyboard...");

            var (keys, groups) = GetKeyboardLayout();
            var expandedOverrides = ExpandGroupsToKeys(colorOverrides, groups);
            var (commandTable, delays) = BuildCommandTable(keys, expandedOverrides);

            try
            {
                _hidWriter.WriteCommands(commandTable, delays);
            }
            catch (KeyboardWriteVerificationException ex)
            {
                // Windows thinks the device is connected and the HID write returned success,
                // but the MCU didn't acknowledge — the USB endpoint has gone stale (typically
                // after a signal-only KVM switch briefly power-cycled the keyboard). Force a
                // full USB re-enumeration to rebuild endpoint state, then retry the sequence
                // once. If the retry also fails, propagate to the outer retry policy.
                _logger.LogWarning(ex, "MCU did not ACK command #{Index}. Forcing USB re-enumeration and retrying...", ex.FailedCommandIndex);

                if (!_hidWriter.TryForceReenumeration())
                    throw;

                _hidWriter.WriteCommands(commandTable, delays);
                _logger.LogInformation("Colors applied successfully after USB re-enumeration");
                return;
            }

            _logger.LogInformation("Colors applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply colors to keyboard");
            throw;
        }
    }

    /// <summary>
    /// Expands group names to individual key mappings
    /// </summary>
    private static Dictionary<string, uint> ExpandGroupsToKeys(Dictionary<string, uint> colorOverrides, Dictionary<string, List<string>> groups)
    {
        return colorOverrides
            .SelectMany(kvp =>
            {
                if (groups.ContainsKey(kvp.Key))
                {
                    return groups[kvp.Key].Select(key => new { Key = key, Color = kvp.Value });
                }
                else
                {
                    return [new { Key = kvp.Key, Color = kvp.Value }];
                }
            })
            .ToDictionary(x => x.Key, x => x.Color);
    }

    /// <summary>
    /// Builds the HID command table with RGB color data for all keys, plus the phase-boundary
    /// delays (in ms) required by the MCU. Sequence matches HP's official SetKeyColor pipeline.
    /// </summary>
    private static (byte[][] commands, int[] delaysBeforeMs) BuildCommandTable(List<string> keys, Dictionary<string, uint> overrides)
    {
        var lines = new (string header, string body, int offset)[]
        {
            (HEADER1, BODY0, 16),  // Keys 0-59, Red
            (HEADER2, BODY1, 16),  // Keys 60-119, Red
            (HEADER3, BODY2, 16),  // Keys 120-179, Red
            (HEADER4, BODY0, 8),   // Keys 0-59, Green
            (HEADER5, BODY1, 8),   // Keys 60-119, Green
            (HEADER6, BODY2, 8),   // Keys 120-179, Green
            (HEADER7, BODY0, 0),   // Keys 0-59, Blue
            (HEADER8, BODY1, 0),   // Keys 60-119, Blue
            (HEADER9, BODY2, 0)    // Keys 120-179, Blue
        };

        const uint DEFAULT_COLOR = 0xFFFFFF; // White

        var commands = new List<byte[]>
        {
            DecodeHex(HEADER_LIGHTING_ON),
            DecodeHex(HEADER0),           // SetUserModeEnable
            DecodeHex(HEADER_KEY_UNLOCK),
        };

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var (header, body, offset) = lines[lineIndex];
            var commandBytes = new List<byte>(DecodeHex(header));

            var bodyBytes = Enumerable.Range(0, body.Length / 2)
                .Select(byteIndex =>
                {
                    if (body[byteIndex * 2] == '0')
                        return (byte)0;

                    int keyIndex = (lineIndex % 3) * 60 + byteIndex;

                    string? keyName = keyIndex < keys.Count ? keys[keyIndex] : null;

                    uint color = keyName != null && overrides.ContainsKey(keyName)
                        ? overrides[keyName]
                        : overrides.ContainsKey("all")
                            ? overrides["all"]
                            : DEFAULT_COLOR;

                    byte component = (byte)((color >> offset) & 0xFF);
                    return component;
                });

            commandBytes.AddRange(bodyBytes);
            commands.Add(commandBytes.ToArray());
        }

        commands.Add(DecodeHex(HEADER_KEY_LOCK));

        // Phase-boundary delays. OGH delays 50ms before mode transitions, but not between
        // consecutive color pages. Index 0 has no pre-delay (it's the first command).
        var delays = new int[commands.Count];
        delays[1] = PhaseDelayMs;                    // before HEADER0 (user mode on)
        delays[2] = PhaseDelayMs;                    // before HEADER_KEY_UNLOCK
        delays[3] = PhaseDelayMs;                    // before first color page
        delays[commands.Count - 1] = PhaseDelayMs;   // before HEADER_KEY_LOCK

        return (commands.ToArray(), delays);
    }

    private static byte[] DecodeHex(string hexString)
    {
        return Enumerable.Range(0, hexString.Length / 2)
            .Select(i => Convert.ToByte(hexString.Substring(i * 2, 2), 16))
            .ToArray();
    }

    private (List<string> keys, Dictionary<string, List<string>> groups) GetKeyboardLayout()
    {
        return (GetKeys(), GetKeyGroups());
    }

    private static List<string> GetKeys()
    {
        return
        [
            // Row 1: ESC, Function keys area
            "esc", "\\", "tab", "capslock", "lshift", "lcontrol", "f12", "«",
            "f9", "9", "o", "l", ",", "<", "????", "leftarrow",

            // Row 2: Number row start, QWERTY row start
            "f1", "1", "q", "a", "????", "windows", "prtscrn", "????",
            "f10", "0", "p", "ç", ".", "????", "enter", "downarrow",

            // Row 3: Continuing number/letter keys
            "f2", "2", "w", "s", "z", "lalt", "sclock", "del",
            "f11", "'", "+", "º", "-", "????", "????", "rightarrow",

            // Row 4
            "f3", "3", "e", "d", "x", "????", "pause", "delete",
            "????", "numpad7", "p1", "????", "numlock", "numpad6", "????", "????",

            // Row 5
            "f4", "4", "r", "f", "c", "????", "insert", "end",
            "????", "numpad8", "p2", "????", "numpad/", "numpad1", "????", "????",

            // Row 6
            "f5", "5", "t", "g", "v", "????", "home", "pgdown",
            "stop", "numpad9", "p3", "????", "numpad*", "numpad2", "????", "????",

            // Row 7
            "f6", "6", "y", "h", "b", "????", "pgup", "rshift",
            "playlast", "????", "p4", "????", "numpad-", "numpad3", "????", "????",

            // Row 8
            "f7", "7", "u", "j", "n", "altgr", "´", "rctrl",
            "play", "numpad4", "p5", "????", "numpad+", "numpad0", "????", "????",

            // Row 9
            "f8", "8", "i", "k", "m", "fn", "~", "uparrow",
            "playnext", "numpad5", "????", "????", "numpadenter", "numpad."
        ];
    }

    private Dictionary<string, List<string>> GetKeyGroups()
    {
        return new Dictionary<string, List<string>>
        {
			["pkeys"]   = ["p1", "p2", "p3", "p4", "p5"],
			["fkeys"]   = ["f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12"],
			["media"]   = ["play", "stop", "playlast", "playnext"],
			["system"]  = ["prtscrn", "sclock", "pause", "insert", "home", "pgup", "del", "delete", "end", "pgdown"],
			["arrows"]  = ["leftarrow", "rightarrow", "uparrow", "downarrow"],
			["numpad"]  = ["numlock", "numpad/", "numpad*", "numpad-", "numpad7", "numpad8", "numpad9", "numpad+", "numpad4", "numpad5", "numpad6", "numpad1", "numpad2", "numpad3", "numpad0", "numpad.", "numpadenter"],
			["fps"]     = ["w", "a", "s", "d"],
			["windows"] = ["windows"]
		};
	}
}
