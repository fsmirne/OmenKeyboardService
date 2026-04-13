namespace OmenKeyboardService;

/// <summary>
/// Maps human-readable key names to HID Usage IDs (USB HID Usage Tables, Keyboard/Keypad Page 0x07)
/// and modifier key names to their bitmask values in the HID modifier byte.
/// </summary>
public static class HidKeyCodes
{
    /// <summary>
    /// Modifier bitmask values for the HID keyboard modifier byte.
    /// Multiple modifiers are combined with bitwise OR.
    /// </summary>
    public static readonly Dictionary<string, byte> Modifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = 0x01, ["lctrl"] = 0x01, ["leftctrl"] = 0x01,
        ["shift"] = 0x02, ["lshift"] = 0x02, ["leftshift"] = 0x02,
        ["alt"] = 0x04, ["lalt"] = 0x04, ["leftalt"] = 0x04,
        ["win"] = 0x08, ["lwin"] = 0x08, ["leftwin"] = 0x08, ["super"] = 0x08, ["meta"] = 0x08,
        ["rctrl"] = 0x10, ["rightctrl"] = 0x10,
        ["rshift"] = 0x20, ["rightshift"] = 0x20,
        ["ralt"] = 0x40, ["rightalt"] = 0x40, ["altgr"] = 0x40,
        ["rwin"] = 0x80, ["rightwin"] = 0x80,
    };

    /// <summary>
    /// Maps key names to HID Usage IDs. Covers standard US keyboard layout.
    /// Values from USB HID Usage Tables 1.4, Section 10 (Keyboard/Keypad Page 0x07).
    /// </summary>
    public static readonly Dictionary<string, byte> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        // Letters
        ["a"] = 0x04, ["b"] = 0x05, ["c"] = 0x06, ["d"] = 0x07, ["e"] = 0x08, ["f"] = 0x09, ["g"] = 0x0A, ["h"] = 0x0B, ["i"] = 0x0C, ["j"] = 0x0D, ["k"] = 0x0E, ["l"] = 0x0F, ["m"] = 0x10, ["n"] = 0x11, ["o"] = 0x12, ["p"] = 0x13, ["q"] = 0x14, ["r"] = 0x15, ["s"] = 0x16, ["t"] = 0x17, ["u"] = 0x18, ["v"] = 0x19, ["w"] = 0x1A, ["x"] = 0x1B, ["y"] = 0x1C, ["z"] = 0x1D,

        // Numbers
        ["1"] = 0x1E, ["2"] = 0x1F, ["3"] = 0x20, ["4"] = 0x21, ["5"] = 0x22, ["6"] = 0x23, ["7"] = 0x24, ["8"] = 0x25, ["9"] = 0x26, ["0"] = 0x27,

        // Common keys
        ["enter"] = 0x28, ["return"] = 0x28, ["esc"] = 0x29, ["escape"] = 0x29, ["backspace"] = 0x2A, ["tab"] = 0x2B, ["space"] = 0x2C, ["spacebar"] = 0x2C,

        // Symbols
        ["-"] = 0x2D, ["minus"] = 0x2D, ["="] = 0x2E, ["equals"] = 0x2E, ["["] = 0x2F, ["leftbracket"] = 0x2F, ["]"] = 0x30, ["rightbracket"] = 0x30, ["\\"] = 0x31, ["backslash"] = 0x31, [";"] = 0x33, ["semicolon"] = 0x33, ["'"] = 0x34, ["quote"] = 0x34, ["apostrophe"] = 0x34, ["`"] = 0x35, ["grave"] = 0x35, ["backtick"] = 0x35, [","] = 0x36, ["comma"] = 0x36, ["."] = 0x37, ["period"] = 0x37, ["dot"] = 0x37, ["/"] = 0x38, ["slash"] = 0x38, ["forwardslash"] = 0x38,

        // Caps/Scroll/Num lock
        ["capslock"] = 0x39, ["caps"] = 0x39,

        // Function keys
        ["f1"] = 0x3A, ["f2"] = 0x3B, ["f3"] = 0x3C, ["f4"] = 0x3D, ["f5"] = 0x3E, ["f6"] = 0x3F, ["f7"] = 0x40, ["f8"] = 0x41, ["f9"] = 0x42, ["f10"] = 0x43, ["f11"] = 0x44, ["f12"] = 0x45, ["f13"] = 0x68, ["f14"] = 0x69, ["f15"] = 0x6A, ["f16"] = 0x6B, ["f17"] = 0x6C, ["f18"] = 0x6D, ["f19"] = 0x6E, ["f20"] = 0x6F, ["f21"] = 0x70, ["f22"] = 0x71, ["f23"] = 0x72, ["f24"] = 0x73,

        // Print/Scroll/Pause
        ["printscreen"] = 0x46, ["prtscr"] = 0x46, ["scrolllock"] = 0x47, ["pause"] = 0x48, ["break"] = 0x48,

        // Navigation
        ["insert"] = 0x49, ["home"] = 0x4A, ["pageup"] = 0x4B, ["pgup"] = 0x4B, ["delete"] = 0x4C, ["del"] = 0x4C, ["end"] = 0x4D, ["pagedown"] = 0x4E, ["pgdn"] = 0x4E, ["pgdown"] = 0x4E,

        // Arrow keys
        ["right"] = 0x4F, ["rightarrow"] = 0x4F, ["left"] = 0x50, ["leftarrow"] = 0x50, ["down"] = 0x51, ["downarrow"] = 0x51, ["up"] = 0x52, ["uparrow"] = 0x52,

        // Numpad
        ["numlock"] = 0x53, ["numpad/"] = 0x54, ["numpaddivide"] = 0x54, ["numpad*"] = 0x55, ["numpadmultiply"] = 0x55, ["numpad-"] = 0x56, ["numpadminus"] = 0x56, ["numpad+"] = 0x57, ["numpadplus"] = 0x57, ["numpadenter"] = 0x58, ["numpad1"] = 0x59, ["numpad2"] = 0x5A, ["numpad3"] = 0x5B, ["numpad4"] = 0x5C, ["numpad5"] = 0x5D, ["numpad6"] = 0x5E, ["numpad7"] = 0x5F, ["numpad8"] = 0x60, ["numpad9"] = 0x61, ["numpad0"] = 0x62, ["numpad."] = 0x63, ["numpaddot"] = 0x63,

        // Application/Menu key
        ["menu"] = 0x65, ["app"] = 0x65, ["contextmenu"] = 0x65,
    };

    /// <summary>
    /// Parses a key combo string like "ctrl+shift+a" into a modifier bitmask and a list of HID key codes.
    /// Modifier keys are accumulated into the bitmask; non-modifier keys become key codes.
    /// </summary>
    public static (byte modifiers, List<byte> keyCodes) ParseKeyCombo(string combo)
    {
        byte modifiers = 0;
        var keyCodes = new List<byte>();

        var parts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (Modifiers.TryGetValue(part, out byte mod))
            {
                modifiers |= mod;
            }
            else if (Keys.TryGetValue(part, out byte keyCode))
            {
                keyCodes.Add(keyCode);
            }
            else
            {
                throw new InvalidOperationException($"Unknown key name: '{part}'. Check config.examples.json for valid key names.");
            }
        }

        if (modifiers == 0 && keyCodes.Count == 0)
            throw new InvalidOperationException($"Key combo '{combo}' resolved to nothing. At least one key or modifier is required.");

        return (modifiers, keyCodes);
    }
}
