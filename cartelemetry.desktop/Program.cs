using Avalonia;
using System;
using CarTelemetry.Core;               // <-- add this
using CarTelemetry.Core.Obd;
using CarTelemetry.Desktop.ViewModels;
using CarTelemetry.Desktop.Services;
using Microsoft.Extensions.DependencyInjection; // <-- add this
using Microsoft.Extensions.Logging;

namespace CarTelemetry.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // build Avalonia app
        var app = BuildAvaloniaApp();

        // --- add this block (dependency injection setup) ---
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
            .AddSingleton<IObdAdapter, MockObdAdapter>()   // swap to Elm327SerialAdapter later
            .AddSingleton<IObdPoller, ObdPoller>()
            .AddSingleton<IDtcService, DtcService>()       // DTC diagnostic service
            
            // Agent/Relay services for transmission
            .AddSingleton<ITelemetryPublisher>(_ => 
            {
                var baseUrl = new Uri("http://localhost:5000"); // switch to HTTPS domain later
                var vehicleId = "Z33-01";
                var sessionId = "dev-local";
                var ingestKey = "super-secret-123";
                return new RelayPublisher(baseUrl, vehicleId, sessionId, ingestKey);
            })
            .AddSingleton<IAgentService, AgentService>()   // Controllable agent service
            
            .AddSingleton<MainViewModel>()
            .AddSingleton<DiagnosticsViewModel>()          // Diagnostics view model
            .BuildServiceProvider();

        App.Services = services; // makes DI container accessible to App.axaml.cs
        // ---------------------------------------------------

        // run app
        app.StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
