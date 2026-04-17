namespace OmenKeyboardService;

/// <summary>
/// Thrown when an LED command was written to the keyboard but the MCU did not respond with the
/// expected acknowledgement bytes (payload[4]=0xEC, payload[5]=0xAC). This typically indicates
/// the USB endpoint went stale — Windows thinks the device is attached, HID writes return success,
/// but the firmware is not actually processing the commands. A USB re-enumeration is usually
/// required to recover.
/// </summary>
public class KeyboardWriteVerificationException : Exception
{
    public int FailedCommandIndex { get; }
    public byte[]? Response { get; }

    public KeyboardWriteVerificationException(int failedCommandIndex, byte[]? response, string message)
        : base(message)
    {
        FailedCommandIndex = failedCommandIndex;
        Response = response;
    }

    public KeyboardWriteVerificationException(int failedCommandIndex, byte[]? response, string message, Exception inner)
        : base(message, inner)
    {
        FailedCommandIndex = failedCommandIndex;
        Response = response;
    }
}
