using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmenKeyboardService;
using System.Runtime.InteropServices;
using System.Text.Json;

// Create a host builder that works on both Windows and Linux
var builder = Host.CreateApplicationBuilder(args);

// Detect the operating system
bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

// Read log level from config.json
LogLevel minimumLogLevel = LogLevel.Warning; // Default to Warning
try
{
    var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    if (File.Exists(configPath))
    {
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<KeyboardConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        });

        if (config?.LogLevel != null && Enum.TryParse<LogLevel>(config.LogLevel, true, out var parsedLevel))
        {
            minimumLogLevel = parsedLevel;
        }
    }
}
catch
{
    // If config reading fails, use default Warning level
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

// Register platform-specific service implementation
if (isWindows)
{
#if WINDOWS
    builder.Services.AddSingleton<IPlatformService, WindowsPlatformService>();
#endif
}
else if (isLinux)
{
#if LINUX
    builder.Services.AddSingleton<IPlatformService, LinuxPlatformService>();
#endif
}
else
{
    throw new PlatformNotSupportedException("This service only supports Windows and Linux platforms.");
}

// Register the background service that will control the keyboard
builder.Services.AddHostedService<KeyboardRgbService>();

// Register the keyboard controller as a singleton
builder.Services.AddSingleton<OmenKeyboardController>();

var host = builder.Build();
await host.RunAsync();
