#if WINDOWS
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace OmenKeyboardService;

/// <summary>
/// Windows-specific platform service implementation
/// Handles Windows power events, session events, and WMI device monitoring
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsPlatformService : IPlatformService
{
    private readonly ILogger<WindowsPlatformService> _logger;
    private ManagementEventWatcher? _deviceArrivalWatcher;
    private SessionNotificationWindow? _sessionWindow;

    // HP Omen keyboard USB identifiers
    private const int VENDOR_ID = 0x03F0;
    private const int PRODUCT_ID = 0x1F41;

    // Deduplication system to prevent rapid successive reapplications
    private DateTime _lastReapplyTime = DateTime.MinValue;
    private const int _deduplicationWindow = 3000; // 3 seconds
    private readonly object _reapplyLock = new object();

    public string PlatformName => "Windows";

    public event EventHandler<ColorReapplyEventArgs>? ColorReapplyRequested;

    public WindowsPlatformService(ILogger<WindowsPlatformService> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        _logger.LogInformation("Initializing Windows platform services...");

        // Set up power management monitoring
        SetupPowerManagement();

        // Set up device monitoring
        SetupDeviceMonitoring();

        _logger.LogInformation("Windows platform services initialized (power events, session events, WMI device monitoring)");
    }

    /// <summary>
    /// Sets up power management event monitoring to restore colors after sleep/wake
    /// </summary>
    private void SetupPowerManagement()
    {
        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // Use Win32 API for session notifications (more reliable for services)
            _sessionWindow = new SessionNotificationWindow(_logger, OnSessionChange, OnDisplayPowerChange);
            _sessionWindow.Initialize();

            _logger.LogInformation("Power management and session monitoring enabled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up power management monitoring");
        }
    }

    /// <summary>
    /// Handles power mode changes (sleep, resume, battery status)
    /// </summary>
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        try
        {
            _logger.LogInformation("Power mode changed: {Mode}", e.Mode);

            // Reapply colors when resuming from sleep or suspend
            if (e.Mode == PowerModes.Resume)
            {
                _logger.LogInformation("System resumed from sleep. Requesting color reapplication...");
                RequestColorReapply("System resumed from sleep", 2000, 5);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling power mode change");
        }
    }

    /// <summary>
    /// Requests a color reapplication with deduplication to prevent rapid successive triggers
    /// </summary>
    /// <param name="reason">Reason for the reapplication request</param>
    /// <param name="delayMs">Delay before applying colors</param>
    /// <param name="retryCount">Number of retry attempts</param>
    private void RequestColorReapply(string reason, int delayMs, int retryCount)
    {
        lock (_reapplyLock)
        {
            var now = DateTime.UtcNow;
            var timeSinceLastReapply = (now - _lastReapplyTime).TotalMilliseconds;

            if (timeSinceLastReapply < _deduplicationWindow)
            {
                _logger.LogInformation(
                    "Skipping color reapply request (reason: {Reason}). " +
                    "Last reapply was {TimeSinceLastReapply}ms ago (within {Window}ms deduplication window)",
                    reason, (int)timeSinceLastReapply, _deduplicationWindow);
                return;
            }

            _logger.LogInformation("Processing color reapply request: {Reason}", reason);
            _lastReapplyTime = now;

            ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
            {
                Reason = reason,
                DelayMs = delayMs,
                RetryCount = retryCount
            });
        }
    }

    /// <summary>
    /// Handles session change events (lock, unlock, remote connect/disconnect)
    /// </summary>
    private void OnSessionChange(SessionChangeReason reason)
    {
        try
        {
            _logger.LogInformation("Session event: {Reason}", reason);

            // Reapply colors when unlocking the session
            if (reason == SessionChangeReason.SessionUnlock)
            {
                _logger.LogInformation("Session unlocked. Requesting color reapplication...");
                RequestColorReapply("Session unlocked", 1000, 5);
            }
            // Handle session logon (fires before unlock, when user logs into account)
            else if (reason == SessionChangeReason.SessionLogon)
            {
                _logger.LogInformation("Session logon detected. Requesting color reapplication...");
                RequestColorReapply("Session logon", 1500, 5);
            }
            // Also handle remote desktop connections
            else if (reason == SessionChangeReason.ConsoleConnect ||
                     reason == SessionChangeReason.RemoteConnect)
            {
                _logger.LogInformation("Console/Remote connected. Requesting color reapplication...");
                RequestColorReapply("Console/Remote connected", 500, 3);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session change");
        }
    }

    /// <summary>
    /// Handles display power change events (fires when display powers on from sleep/off state)
    /// </summary>
    private void OnDisplayPowerChange()
    {
        try
        {
            _logger.LogInformation("Display powered on. Requesting color reapplication...");
            RequestColorReapply("Display powered on", 500, 3);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling display power change");
        }
    }

    /// <summary>
    /// Sets up WMI event monitoring to detect keyboard reconnection (e.g., KVM switch, USB replug)
    /// </summary>
    private void SetupDeviceMonitoring()
    {
        try
        {
            // WMI query to watch for USB device arrival events
            // This fires immediately when any USB device is connected
            var query = new WqlEventQuery(
                "SELECT * FROM __InstanceCreationEvent WITHIN 1 " +
                "WHERE TargetInstance ISA 'Win32_PnPEntity' " +
                "AND TargetInstance.DeviceID LIKE 'HID\\\\VID_03F0&PID_1F41%'");

            _deviceArrivalWatcher = new ManagementEventWatcher(query);
            _deviceArrivalWatcher.EventArrived += OnDeviceArrived;
            _deviceArrivalWatcher.Start();

            _logger.LogInformation("WMI device monitoring enabled. Will detect keyboard reconnection (KVM switch, USB replug, etc.)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up WMI device monitoring. Keyboard reconnection detection will not work.");
        }
    }

    /// <summary>
    /// Handles USB device arrival events and reapplies colors when the keyboard reconnects
    /// </summary>
    private void OnDeviceArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            _logger.LogInformation("HP Omen keyboard reconnected (KVM switch or USB replug detected)");
            RequestColorReapply("HP Omen keyboard reconnected", 1000, 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling device arrival");
        }
    }

    public void Dispose()
    {
        if (_deviceArrivalWatcher != null)
        {
            _deviceArrivalWatcher.Stop();
            _deviceArrivalWatcher.Dispose();
        }

        _sessionWindow?.Dispose();

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    /// <summary>
    /// Session change reasons from WTS_SESSION_CHANGE
    /// </summary>
    private enum SessionChangeReason
    {
        ConsoleConnect = 0x1,
        ConsoleDisconnect = 0x2,
        RemoteConnect = 0x3,
        RemoteDisconnect = 0x4,
        SessionLogon = 0x5,
        SessionLogoff = 0x6,
        SessionLock = 0x7,
        SessionUnlock = 0x8,
        SessionRemoteControl = 0x9
    }

    /// <summary>
    /// Message-only window for receiving WTS session notifications
    /// This is required for Windows services to reliably receive session change events
    /// </summary>
    private class SessionNotificationWindow : IDisposable
    {
        private const int WM_WTSSESSION_CHANGE = 0x02B1;
        private const int WM_POWERBROADCAST = 0x0218;
        private const int PBT_POWERSETTINGCHANGE = 0x8013;
        private const int NOTIFY_FOR_ALL_SESSIONS = 1;

        // Display power state GUIDs
        private static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new Guid("6fe69556-704a-47a0-8f24-c28d936fda47");
        private static readonly Guid GUID_MONITOR_POWER_ON = new Guid("02731015-4510-4526-99e6-e5a17ebd1aea");
        private static readonly Guid GUID_SESSION_USER_PRESENCE = new Guid("3c0f4548-c03f-4c4d-b9f2-237ede686376");

        private readonly ILogger _logger;
        private readonly Action<SessionChangeReason> _onSessionChange;
        private readonly Action? _onDisplayPowerChange;
        private IntPtr _hwnd;
        private WndProcDelegate? _wndProcDelegate;
        private IntPtr _displayPowerNotifyHandle;
        private IntPtr _monitorPowerNotifyHandle;
        private IntPtr _userPresenceNotifyHandle;

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent,
            IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POWERBROADCAST_SETTING
        {
            public Guid PowerSetting;
            public uint DataLength;
            public byte Data;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public SessionNotificationWindow(ILogger logger, Action<SessionChangeReason> onSessionChange, Action? onDisplayPowerChange = null)
        {
            _logger = logger;
            _onSessionChange = onSessionChange;
            _onDisplayPowerChange = onDisplayPowerChange;
        }

        public void Initialize()
        {
            try
            {
                // Create a delegate for the window procedure and keep it alive
                _wndProcDelegate = WndProc;

                // Register window class
                var wndClass = new WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                    hInstance = GetModuleHandle(null),
                    lpszClassName = "OmenKeyboardSessionNotification"
                };

                ushort classAtom = RegisterClassEx(ref wndClass);
                if (classAtom == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger.LogWarning("Failed to register window class for session notifications (error: {Error})", error);
                    return;
                }

                // Create message-only window (HWND_MESSAGE = -3)
                _hwnd = CreateWindowEx(
                    0, "OmenKeyboardSessionNotification", "OmenKeyboardSessionWindow", 0,
                    0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);

                if (_hwnd == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger.LogWarning("Failed to create session notification window (error: {Error})", error);
                    return;
                }

                // Register for session notifications
                if (!WTSRegisterSessionNotification(_hwnd, NOTIFY_FOR_ALL_SESSIONS))
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger.LogWarning("Failed to register for session notifications (error: {Error})", error);
                    return;
                }

                // Register for display power notifications
                if (_onDisplayPowerChange != null)
                {
                    var displayGuid = GUID_CONSOLE_DISPLAY_STATE;
                    _displayPowerNotifyHandle = RegisterPowerSettingNotification(_hwnd, ref displayGuid, 0);
                    if (_displayPowerNotifyHandle == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        _logger.LogWarning("Failed to register for display power notifications (error: {Error})", error);
                    }
                    else
                    {
                        _logger.LogInformation("Registered for display power notifications");
                    }

                    var monitorGuid = GUID_MONITOR_POWER_ON;
                    _monitorPowerNotifyHandle = RegisterPowerSettingNotification(_hwnd, ref monitorGuid, 0);
                    if (_monitorPowerNotifyHandle == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        _logger.LogWarning("Failed to register for monitor power notifications (error: {Error})", error);
                    }
                    else
                    {
                        _logger.LogInformation("Registered for monitor power notifications");
                    }

                    // Register for user presence notifications (detects user activity after idle/away)
                    var userPresenceGuid = GUID_SESSION_USER_PRESENCE;
                    _userPresenceNotifyHandle = RegisterPowerSettingNotification(_hwnd, ref userPresenceGuid, 0);
                    if (_userPresenceNotifyHandle == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        _logger.LogWarning("Failed to register for user presence notifications (error: {Error})", error);
                    }
                    else
                    {
                        _logger.LogInformation("Registered for user presence notifications");
                    }
                }

                _logger.LogInformation("Win32 session notification window registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize session notification window");
            }
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_WTSSESSION_CHANGE)
            {
                try
                {
                    var reason = (SessionChangeReason)wParam.ToInt32();
                    _onSessionChange(reason);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing session change notification");
                }
            }
            else if (msg == WM_POWERBROADCAST && wParam.ToInt32() == PBT_POWERSETTINGCHANGE)
            {
                try
                {
                    // Parse POWERBROADCAST_SETTING structure
                    var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);

                    // Check if this is a display or monitor power event
                    if (setting.PowerSetting == GUID_CONSOLE_DISPLAY_STATE ||
                        setting.PowerSetting == GUID_MONITOR_POWER_ON)
                    {
                        // Data field contains the power state: 0 = off, 1 = on, 2 = dimmed
                        if (setting.Data == 1)
                        {
                            _logger.LogInformation("Display powered ON. Requesting color reapplication...");
                            _onDisplayPowerChange?.Invoke();
                        }
                        else
                        {
                            _logger.LogDebug("Display power state changed to: {State}", setting.Data);
                        }
                    }
                    // Check if this is a user presence event
                    else if (setting.PowerSetting == GUID_SESSION_USER_PRESENCE)
                    {
                        // Data field: 0 = user absent/away, 2 = user present/active
                        if (setting.Data == 2)
                        {
                            _logger.LogInformation("User presence detected (returned from idle). Requesting color reapplication...");
                            _onDisplayPowerChange?.Invoke();
                        }
                        else
                        {
                            _logger.LogDebug("User presence state changed to: {State}", setting.Data);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing power broadcast notification");
                }
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            // Unregister power notifications
            if (_displayPowerNotifyHandle != IntPtr.Zero)
            {
                UnregisterPowerSettingNotification(_displayPowerNotifyHandle);
                _displayPowerNotifyHandle = IntPtr.Zero;
            }

            if (_monitorPowerNotifyHandle != IntPtr.Zero)
            {
                UnregisterPowerSettingNotification(_monitorPowerNotifyHandle);
                _monitorPowerNotifyHandle = IntPtr.Zero;
            }

            if (_userPresenceNotifyHandle != IntPtr.Zero)
            {
                UnregisterPowerSettingNotification(_userPresenceNotifyHandle);
                _userPresenceNotifyHandle = IntPtr.Zero;
            }

            if (_hwnd != IntPtr.Zero)
            {
                WTSUnRegisterSessionNotification(_hwnd);
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }

            if (_wndProcDelegate != null)
            {
                var hInstance = GetModuleHandle(null);
                UnregisterClass("OmenKeyboardSessionNotification", hInstance);
                _wndProcDelegate = null;
            }
        }
    }
}
#endif
