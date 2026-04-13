namespace OmenKeyboardService;

/// <summary>
/// Abstracts HID device access for the keyboard.
/// Platform-specific implementations handle device discovery and I/O.
/// </summary>
public interface IKeyboardHidWriter
{
    void WriteCommands(byte[][] commands);

    /// <summary>
    /// Sends multiple commands sequentially, reading a response after each.
    /// The reportId parameter selects the HID Report ID: 0 for LED commands, 1 for macro commands.
    /// </summary>
    byte[][] WriteAndReadAll(byte[][] commands, byte reportId = 0, int readTimeoutMs = 2000);
}
