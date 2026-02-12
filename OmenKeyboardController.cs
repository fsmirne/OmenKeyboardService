using HidSharp;

namespace OmenKeyboardService;

/// <summary>
/// Controller for HP Omen keyboard RGB LED control
/// Ported from Sequencer.linq - sends HID commands to control individual key colors
/// </summary>
public class OmenKeyboardController
{
    private readonly ILogger<OmenKeyboardController> _logger;

    // HID protocol headers - magic bytes that the keyboard firmware expects
    // HEADER0 is the initialization command sent before any color data
    private const string HEADER0 = "04000200fcea00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

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

    // Body templates define which keys are controlled in each packet
    // 'ff' = active key slot (will be replaced with color data)
    // '00' = unused/padding slot
    private const string BODY0 = "ffffffffffffffffffffffffffff00ffffffffff00ffff00ffffffffff00ffffffffffffffffffffffffffffff0000ffffffffffff00ffff00ffff00";
    private const string BODY1 = "ffff0000ffffffffffffffff00ffff00ffff0000ffffffffff00ffffffffff00ffff0000ffffffffff00ffffff00ff00ffff0000ffffffffffffffff";
    private const string BODY2 = "ffffff00ffff0000ffffffffffffffffffff0000ffff0000000000000000000000000000000000000000000000000000000000000000000000000000";

    // HP Omen keyboard USB identifiers
    private const int VENDOR_ID = OmenKeyboardConstants.VendorId;
    private const int PRODUCT_ID = OmenKeyboardConstants.ProductId;

    public OmenKeyboardController(ILogger<OmenKeyboardController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Applies color settings to the keyboard based on key group mappings
    /// </summary>
    /// <param name="colorOverrides">Dictionary mapping key/group names to RGB color values</param>
    public void ApplyColors(Dictionary<string, uint> colorOverrides)
    {
        try
        {
            _logger.LogInformation("Applying colors to keyboard...");

            // Get the keyboard layout
            var (keys, groups) = GetKeyboardLayout();

            // Expand group names to individual keys
            var expandedOverrides = ExpandGroupsToKeys(colorOverrides, groups);

            // Build the HID command table
            var commandTable = BuildCommandTable(keys, expandedOverrides);

            // Open the keyboard device
            var device = OpenKeyboardDevice();

            using var stream = device.Open();

            // Send each command to the keyboard
            foreach (var command in commandTable)
            {
                WriteHidCommand(device, stream, command.ToArray());
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
    /// Opens the HP Omen keyboard HID device
    /// </summary>
    private HidDevice OpenKeyboardDevice()
    {
        var deviceList = DeviceList.Local;
        var devices = deviceList.GetHidDevices(VENDOR_ID, PRODUCT_ID).ToList();

        _logger.LogDebug("Searching for HP Omen keyboard (VID: 0x{VendorId:X4}, PID: 0x{ProductId:X4})...", VENDOR_ID, PRODUCT_ID);
        _logger.LogDebug("Found {Count} matching HID device(s)", devices.Count);

        if (!devices.Any())
        {
            _logger.LogWarning("HP Omen keyboard not found. Keyboard may not be connected or may still be initializing.");
            throw new InvalidOperationException($"HP Omen keyboard not found (VID: 0x{VENDOR_ID:X4}, PID: 0x{PRODUCT_ID:X4}). Please ensure the keyboard is connected and initialized.");
        }

        // Select the device with the longest path (most specific interface)
        var device = devices.OrderByDescending(x => x.DevicePath).First();
        _logger.LogDebug("Selected keyboard device: {DevicePath}", device.DevicePath);

        return device;
    }

    /// <summary>
    /// Writes a HID command to the keyboard device
    /// </summary>
    private static void WriteHidCommand(HidDevice device, HidStream stream, byte[] command)
    {
        int maxSize = device.GetMaxOutputReportLength();
        if (maxSize <= 0)
        {
            throw new InvalidOperationException("Device does not support output reports.");
        }

        // Prepare buffer with report ID at position 0
        var buffer = new byte[maxSize];
        buffer[0] = 0; // Report ID (always 0 for this device)

        // Copy the command data starting at position 1
        Array.Copy(command, 0, buffer, 1, Math.Min(command.Length, maxSize - 1));

        stream.Write(buffer);
    }

    /// <summary>
    /// Expands group names to individual key mappings
    /// </summary>
    private static Dictionary<string, uint> ExpandGroupsToKeys(Dictionary<string, uint> colorOverrides, Dictionary<string, List<string>> groups)
    {
        return colorOverrides
            .SelectMany(kvp =>
            {
                // If it's a group, expand to all keys in that group
                if (groups.ContainsKey(kvp.Key))
                {
                    return groups[kvp.Key].Select(key => new { Key = key, Color = kvp.Value });
                }
                // Otherwise, it's a single key
                else
                {
                    return [new { Key = kvp.Key, Color = kvp.Value }];
                }
            })
            .ToDictionary(x => x.Key, x => x.Color);
    }

    /// <summary>
    /// Builds the HID command table with RGB color data for all keys
    /// </summary>
    private static List<List<byte>> BuildCommandTable(List<string> keys, Dictionary<string, uint> overrides)
    {
        // Define the 9 command line structures
        // Each tuple contains: (header, body template, bit offset for color component)
        var lines = new List<(string header, string body, int offset)>
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

        // Start with the initialization header
        var result = new List<List<byte>>
        {
            new(DecodeHex(HEADER0))
        };

        // Build each of the 9 command lines
        var commandLines = lines.Select((line, lineIndex) =>
        {
            var (header, body, offset) = line;

            // Start with the decoded header bytes
            var commandBytes = new List<byte>(DecodeHex(header));

            // Process the body template to add color data
            var bodyBytes = Enumerable.Range(0, body.Length / 2)
                .Select(byteIndex =>
                {
                    // Check if this slot is active ('ff') or padding ('00')
                    if (body[byteIndex * 2] == '0')
                        return (byte)0;

                    // Calculate which key this byte position represents
                    int keyIndex = (lineIndex % 3) * 60 + byteIndex;

                    // Get the key name from the layout
                    string? keyName = keyIndex < keys.Count ? keys[keyIndex] : null;

                    // Determine the color for this key
                    uint color = keyName != null && overrides.ContainsKey(keyName)
                        ? overrides[keyName]
                        : overrides.ContainsKey("all")
                            ? overrides["all"]
                            : DEFAULT_COLOR;

                    // Extract the appropriate color component (R, G, or B)
                    byte component = (byte)((color >> offset) & 0xFF);
                    return component;
                });

            commandBytes.AddRange(bodyBytes);
            return commandBytes;
        });

        result.AddRange(commandLines);
        return result;
    }

    /// <summary>
    /// Converts a hex string to a byte array
    /// </summary>
    private static byte[] DecodeHex(string hexString)
    {
        return Enumerable.Range(0, hexString.Length / 2)
            .Select(i => Convert.ToByte(hexString.Substring(i * 2, 2), 16))
            .ToArray();
    }

    /// <summary>
    /// Gets the keyboard layout configuration
    /// </summary>
    private (List<string> keys, Dictionary<string, List<string>> groups) GetKeyboardLayout()
    {
        return (GetKeys(), GetKeyGroups());
    }

    /// <summary>
    /// Returns the complete keyboard layout mapping
    /// </summary>
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

    /// <summary>
    /// Defines convenient key groups for bulk color assignment
    /// </summary>
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
