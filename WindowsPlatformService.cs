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
            _sessionWindow = new SessionNotificationWindow(_logger, OnSessionChange);
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

                ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
                {
                    Reason = "System resumed from sleep",
                    DelayMs = 2000,  // Longer delay for hardware initialization
                    RetryCount = 5
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling power mode change");
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

                ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
                {
                    Reason = "Session unlocked",
                    DelayMs = 1000,
                    RetryCount = 5
                });
            }
            // Also handle remote desktop connections
            else if (reason == SessionChangeReason.ConsoleConnect ||
                     reason == SessionChangeReason.RemoteConnect)
            {
                _logger.LogInformation("Console/Remote connected. Requesting color reapplication...");

                ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
                {
                    Reason = "Console/Remote connected",
                    DelayMs = 500,
                    RetryCount = 3
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session change");
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

            ColorReapplyRequested?.Invoke(this, new ColorReapplyEventArgs
            {
                Reason = "HP Omen keyboard reconnected",
                DelayMs = 1000,
                RetryCount = 5
            });
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
        private const int NOTIFY_FOR_ALL_SESSIONS = 1;

        private readonly ILogger _logger;
        private readonly Action<SessionChangeReason> _onSessionChange;
        private IntPtr _hwnd;
        private WndProcDelegate? _wndProcDelegate;

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

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public SessionNotificationWindow(ILogger logger, Action<SessionChangeReason> onSessionChange)
        {
            _logger = logger;
            _onSessionChange = onSessionChange;
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

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
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
