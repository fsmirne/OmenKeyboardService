namespace OmenKeyboardService;

/// <summary>
/// Abstracts HID device access for the keyboard.
/// Platform-specific implementations handle device discovery and I/O.
/// </summary>
public interface IKeyboardHidWriter
{
    /// <summary>
    /// Writes a sequence of HID commands to the keyboard (Report ID 0).
    /// If <paramref name="delaysBeforeMs"/> is supplied, element i specifies how many
    /// milliseconds to wait before writing <c>commands[i]</c> (the first element applies
    /// before the first command). A null array or a shorter array implies zero delay for
    /// the remaining commands.
    /// </summary>
    void WriteCommands(byte[][] commands, int[]? delaysBeforeMs = null);

    /// <summary>
    /// Sends multiple commands sequentially, reading a response after each.
    /// The reportId parameter selects the HID Report ID: 0 for LED commands, 1 for macro commands.
    /// </summary>
    byte[][] WriteAndReadAll(byte[][] commands, byte reportId = 0, int readTimeoutMs = 2000);
}
