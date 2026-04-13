namespace OmenKeyboardService;

/// <summary>
/// Controller for programming macro keys (P1-P5, FN+P1-FN+P5) on the HP Omen Sequencer keyboard.
/// Uses the "Woodstock" protocol from HP's McuSDK — NOT the older "Dragon" protocol.
///
/// Key protocol details:
///   - HID Report ID must be 1 (not 0 which is used for LED commands)
///   - Length command uses BLength_low=4 with data length + loop count
///   - Slots must be cleared before writing new data (MCU won't overwrite)
///   - Data chunks encode both size and chunk index in BLength bytes
/// </summary>
public class OmenMacroController
{
    private readonly ILogger<OmenMacroController> _logger;
    private readonly IKeyboardHidWriter _hidWriter;

    private const byte CMD_SET_MACRO = 0x02;
    private const int DATA_CHUNK_SIZE = 60;

    /// <summary>
    /// Maps config key names to their MacroKeyType values (from McuSDK MacroKeyType enum).
    /// McuIndex = MacroKeyType - 1 (used as byte 0 of MCU data).
    /// DataKey = 64 + MacroKeyType (used as Index byte in data chunks).
    /// </summary>
    public static readonly Dictionary<string, MacroKeySlot> MacroKeySlots = new(StringComparer.OrdinalIgnoreCase)
    {
        ["p1"] = new(MacroKeyType: 1, McuIndex: 0),
        ["p2"] = new(MacroKeyType: 2, McuIndex: 1),
        ["p3"] = new(MacroKeyType: 3, McuIndex: 2),
        ["p4"] = new(MacroKeyType: 4, McuIndex: 3),
        ["p5"] = new(MacroKeyType: 5, McuIndex: 4),
        ["fn+p1"] = new(MacroKeyType: 7, McuIndex: 6),
        ["fn+p2"] = new(MacroKeyType: 8, McuIndex: 7),
        ["fn+p3"] = new(MacroKeyType: 9, McuIndex: 8),
        ["fn+p4"] = new(MacroKeyType: 10, McuIndex: 9),
        ["fn+p5"] = new(MacroKeyType: 11, McuIndex: 10),
    };

    public OmenMacroController(ILogger<OmenMacroController> logger, IKeyboardHidWriter hidWriter)
    {
        _logger = logger;
        _hidWriter = hidWriter;
    }

    /// <summary>
    /// Programs configured macro keys on the MCU. Each slot is cleared first, then written with new data.
    /// All commands are batched into a single HID session.
    /// </summary>
    public void ApplyMacros(Dictionary<string, MacroDefinition> macros)
    {
        _logger.LogInformation("Applying macro key assignments...");

        var allCommands = new List<byte[]>();

        foreach (var (keyName, macro) in macros)
        {
            var normalizedKey = keyName.ToLowerInvariant();
            if (!MacroKeySlots.TryGetValue(normalizedKey, out var slot))
            {
                _logger.LogWarning("Unknown macro key '{KeyName}', skipping", keyName);
                continue;
            }

            try
            {
                // Clear the slot first (MCU requires this before overwriting)
                allCommands.AddRange(BuildClearCommands(slot));

                // Write the new macro
                var mcuData = BuildMcuData(slot.McuIndex, macro);
                allCommands.AddRange(BuildWriteCommands(slot, mcuData, loopCount: 1));

                _logger.LogInformation("Queued {KeyName}: {Description}", normalizedKey, DescribeMacro(macro));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build macro for key '{KeyName}'", keyName);
            }
        }

        if (allCommands.Count > 0)
        {
            _hidWriter.WriteAndReadAll(allCommands.ToArray(), reportId: 1);
            _logger.LogInformation("Macro key assignments applied ({Count} commands sent)", allCommands.Count);
        }
    }

    /// <summary>
    /// Builds commands to clear/reset a macro slot (RestoreMacroToDefault).
    /// Sends empty macro data [mcuIndex, 0x02, 0x00] with loopCount=0.
    /// </summary>
    private static List<byte[]> BuildClearCommands(MacroKeySlot slot)
    {
        byte[] emptyData = [slot.McuIndex, 0x02, 0x00];
        return BuildWriteCommands(slot, emptyData, loopCount: 0);
    }

    /// <summary>
    /// Builds the HID command sequence for writing macro data to a slot.
    /// Uses the Woodstock protocol: BLength_low=4 length command with loop count,
    /// and chunk-indexed data commands.
    /// </summary>
    private static List<byte[]> BuildWriteCommands(MacroKeySlot slot, byte[] mcuData, int loopCount)
    {
        var commands = new List<byte[]>();
        int dataLength = mcuData.Length;

        // Length command: [CMD_SET_MACRO, macroKeyType, 0x04, 0x00, dataLen_low, dataLen_high, loop_low, loop_high]
        var lengthCmd = new byte[64];
        lengthCmd[0] = CMD_SET_MACRO;
        lengthCmd[1] = (byte)slot.MacroKeyType;
        lengthCmd[2] = 0x04; // BLength_low = 4 (Woodstock format)
        lengthCmd[4] = (byte)(dataLength % 256);
        lengthCmd[5] = (byte)(dataLength / 256);
        lengthCmd[6] = (byte)(loopCount % 256);
        lengthCmd[7] = (byte)(loopCount / 256);
        commands.Add(lengthCmd);

        // Data chunks: [CMD_SET_MACRO, 64+macroKeyType, bLenLow, bLenHigh, ...data...]
        byte dataIndex = (byte)(64 + slot.MacroKeyType);
        int numFullChunks = dataLength / DATA_CHUNK_SIZE;
        int remainder = dataLength % DATA_CHUNK_SIZE;

        for (int i = 0; i <= numFullChunks; i++)
        {
            int chunkSize;
            byte bLenLow, bLenHigh;

            if (i == numFullChunks)
            {
                if (remainder == 0) break;
                chunkSize = remainder;
            }
            else
            {
                chunkSize = DATA_CHUNK_SIZE;
            }

            // Encode chunk size and index: low 6 bits = size, upper bits = chunk index
            bLenLow = (byte)(chunkSize | (i << 6));
            bLenHigh = (byte)(i >> 2);

            var dataCmd = new byte[64];
            dataCmd[0] = CMD_SET_MACRO;
            dataCmd[1] = dataIndex;
            dataCmd[2] = bLenLow;
            dataCmd[3] = bLenHigh;
            Array.Copy(mcuData, i * DATA_CHUNK_SIZE, dataCmd, 4, chunkSize);
            commands.Add(dataCmd);
        }

        return commands;
    }

    /// <summary>
    /// Builds the MCU macro data byte array for a single key assignment.
    /// Format: [mcuIndex, 0x02, actionCount, ...actionRecords...]
    /// </summary>
    private static byte[] BuildMcuData(byte mcuIndex, MacroDefinition macro)
    {
        var steps = GetStepsFromMacro(macro);
        var data = new List<byte> { mcuIndex, 0x02, 0x00 }; // header: index, has-actions flag, action count placeholder

        byte currentModifiers = 0;
        var currentKeys = new List<byte>();
        int actionCount = 0;

        foreach (var step in steps)
        {
            var (modifiers, keyCodes) = HidKeyCodes.ParseKeyCombo(step.Keys);

            // KeyDown: press the new keys/modifiers
            currentModifiers |= modifiers;
            foreach (var kc in keyCodes)
            {
                if (!currentKeys.Contains(kc))
                    currentKeys.Add(kc);
            }
            data.AddRange(BuildActionRecord(currentModifiers, currentKeys, 0));
            actionCount++;

            // KeyUp: release everything, with delay if specified
            currentModifiers = 0;
            currentKeys.Clear();
            data.AddRange(BuildActionRecord(0, currentKeys, (ushort)step.DelayMs));
            actionCount++;
        }

        data[2] = (byte)actionCount;
        return data.ToArray();
    }

    /// <summary>
    /// Builds a single action record: [0x70|byteCount, modifiers, keyCodes..., delayLow, delayHigh]
    /// </summary>
    private static byte[] BuildActionRecord(byte modifiers, List<byte> keyCodes, ushort delayMs)
    {
        var record = new List<byte> { 0x70, modifiers };

        if (keyCodes.Count == 0)
            record.Add(0x00);

        foreach (var kc in keyCodes)
            record.Add(kc);

        record.Add((byte)(delayMs & 0xFF));
        record.Add((byte)((delayMs >> 8) & 0xFF));

        record[0] = (byte)(0x70 | (record.Count & 0x0F));
        return record.ToArray();
    }

    private static List<MacroStep> GetStepsFromMacro(MacroDefinition macro)
    {
        if (macro.Sequence != null)
            return macro.Sequence;
        return [new MacroStep { Keys = macro.Keys! }];
    }

    private static string DescribeMacro(MacroDefinition macro)
    {
        if (macro.Keys != null)
            return macro.Keys;
        return string.Join(" → ", macro.Sequence!.Select(s => s.DelayMs > 0 ? $"{s.Keys} ({s.DelayMs}ms)" : s.Keys));
    }
}

public record MacroKeySlot(int MacroKeyType, byte McuIndex);
