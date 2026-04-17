namespace OmenKeyboardService;

/// <summary>
/// Abstracts HID device access for the keyboard.
/// Platform-specific implementations handle device discovery and I/O.
/// </summary>
public interface IKeyboardHidWriter
{
    /// <summary>
    /// Writes a sequence of HID commands to the keyboard (Report ID 0) and verifies that the
    /// MCU acknowledged each one (expected response bytes [4]=0xEC, [5]=0xAC). Throws
    /// <see cref="KeyboardWriteVerificationException"/> on the first command that fails to ACK.
    /// If <paramref name="delaysBeforeMs"/> is supplied, element i specifies how many
    /// milliseconds to wait before writing <c>commands[i]</c> (the first element applies before
    /// the first command). A null array or a shorter array implies zero delay for the rest.
    /// </summary>
    void WriteCommands(byte[][] commands, int[]? delaysBeforeMs = null);

    /// <summary>
    /// Attempts to programmatically force the OS to re-enumerate the keyboard's USB device
    /// (equivalent to a physical unplug/replug). Returns true if the operation succeeded and
    /// the device reappeared within a reasonable timeout. Platform-specific: Windows uses
    /// SetupAPI; Linux currently returns false.
    /// </summary>
    bool TryForceReenumeration();

    /// <summary>
    /// Sends multiple commands sequentially, reading a response after each.
    /// The reportId parameter selects the HID Report ID: 0 for LED commands, 1 for macro commands.
    /// </summary>
    byte[][] WriteAndReadAll(byte[][] commands, byte reportId = 0, int readTimeoutMs = 2000);
}
