using OmenKeyboardService;
using System.Runtime.InteropServices;

// Create a host builder that works on both Windows and Linux
var builder = Host.CreateApplicationBuilder(args);

// Detect the operating system
bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

// Read log level from config.json
LogLevel minimumLogLevel = LogLevel.Warning; // Default to Warning
try
{
    var configProvider = new KeyboardConfigProvider();
    var config = configProvider.LoadOrCreateDefault();
    if (config.LogLevel != null && Enum.TryParse<LogLevel>(config.LogLevel, true, out var parsedLevel))
    {
        minimumLogLevel = parsedLevel;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to read config for log level, using default Warning: {ex.Message}");
}

// Configure logging based on platform
builder.Logging.ClearProviders();

if (isWindows)
{
#if WINDOWS
    // Windows: Log to Event Log
    builder.Logging.AddEventLog(new Microsoft.Extensions.Logging.EventLog.EventLogSettings
    {
        SourceName = "HP Omen Keyboard RGB Service",
        LogName = "Application"
    });
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>(level => level >= minimumLogLevel);
#endif
}
else
{
    // Linux/macOS: Log to console (captured by systemd journal)
    builder.Logging.AddSimpleConsole(options =>
    {
        options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
    });
}

// Set logging levels from config
builder.Logging.SetMinimumLevel(minimumLogLevel);

// Configure platform-specific service hosting
if (isWindows)
{
#if WINDOWS
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "HP Omen Keyboard RGB Service";
    });
#endif
}
else if (isLinux)
{
#if LINUX
    builder.Services.AddSystemd();
#endif
}

// Register platform-specific service implementations
if (isWindows)
{
#if WINDOWS
    builder.Services.AddSingleton<IPlatformService, WindowsPlatformService>();
    builder.Services.AddSingleton<IKeyboardHidWriter, WindowsKeyboardHidWriter>();
#endif
}
else if (isLinux)
{
#if LINUX
    builder.Services.AddSingleton<IPlatformService, LinuxPlatformService>();
    builder.Services.AddSingleton<IKeyboardHidWriter, LinuxKeyboardHidWriter>();
#endif
}
else
{
    throw new PlatformNotSupportedException("This service only supports Windows and Linux platforms.");
}

// Register the background service that will control the keyboard
builder.Services.AddHostedService<KeyboardRgbService>();

// Register the keyboard controllers as singletons
builder.Services.AddSingleton<OmenKeyboardController>();
builder.Services.AddSingleton<OmenMacroController>();

// Register config provider
builder.Services.AddSingleton<KeyboardConfigProvider>();

var host = builder.Build();
await host.RunAsync();
