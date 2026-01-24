using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using OmenKeyboardService;

// Create a Windows service host
var builder = Host.CreateApplicationBuilder(args);

// Configure logging to write to Windows Event Log
builder.Logging.ClearProviders();
builder.Logging.AddEventLog(new EventLogSettings
{
    SourceName = "HP Omen Keyboard RGB Service",
    LogName = "Application"
});

// Set logging levels
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure the service to run as a Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "HP Omen Keyboard RGB Service";
});

// Register the background service that will control the keyboard
builder.Services.AddHostedService<KeyboardRgbService>();

// Register the keyboard controller as a singleton
builder.Services.AddSingleton<OmenKeyboardController>();

var host = builder.Build();
await host.RunAsync();
