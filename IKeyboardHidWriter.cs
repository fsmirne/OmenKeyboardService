namespace OmenKeyboardService;

/// <summary>
/// Abstracts HID device access for writing commands to the keyboard.
/// Platform-specific implementations handle device discovery and I/O.
/// </summary>
public interface IKeyboardHidWriter
{
    void WriteCommands(byte[][] commands);
}
