using Avalonia;
using System;
using CarTelemetry.Core;               
using CarTelemetry.Core.Obd;
using CarTelemetry.Desktop.ViewModels;
using CarTelemetry.Desktop.Services;
using CarTelemetry.Desktop.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace CarTelemetry.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // build Avalonia app
        var app = BuildAvaloniaApp();

        // Build configuration with smart environment detection
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                            ?? DetectEnvironment();
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .Build();

        // Create early logger for startup messages
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().AddConfiguration(configuration.GetSection("Logging")));
        var startupLogger = loggerFactory.CreateLogger("Startup");
        
        startupLogger.LogInformation("🔧 Running in {Environment} environment", environmentName);
        
        // Also show in debug output for development
        System.Diagnostics.Debug.WriteLine($"🔧 CarTelemetry: Running in {environmentName} environment");

        // Get relay configuration
        var relayConfig = new RelayConfiguration();
        configuration.GetSection("Relay").Bind(relayConfig);

        // --- add this block (dependency injection setup) ---
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole().AddConfiguration(configuration.GetSection("Logging")))
            .AddSingleton<IObdAdapter, MockObdAdapter>()   // swap to Elm327SerialAdapter later
            .AddSingleton<IObdPoller, ObdPoller>()
            .AddSingleton<IDtcDescriptionService, DtcDescriptionService>() // DTC description lookup
            .AddSingleton<IDtcService, DtcService>()       // DTC diagnostic service
            
            // Agent/Relay services for transmission
            .AddSingleton<ITelemetryPublisher>(_ => 
            {
                var baseUrl = new Uri(relayConfig.BaseUrl);
                return new RelayPublisher(baseUrl, relayConfig.VehicleId, relayConfig.SessionId, relayConfig.IngestKey);
            })
            .AddSingleton<IAgentService, AgentService>()   // Controllable agent service
            
            .AddSingleton<MainViewModel>()
            .AddSingleton<DiagnosticsViewModel>()          // Diagnostics view model
            .AddSingleton<SettingsViewModel>()             // Settings view model
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

    /// <summary>
    /// Smart environment detection based on platform and hardware
    /// </summary>
    private static string DetectEnvironment()
    {
        // Check for manual environment override
        var manualEnv = Environment.GetEnvironmentVariable("Z33_ENVIRONMENT");
        if (!string.IsNullOrEmpty(manualEnv))
        {
            // Note: Can't use logger here as it's not created yet, but this will show in logs later
            return manualEnv;
        }

        // Check if running on ARM (Raspberry Pi, etc.)
        if (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == 
            System.Runtime.InteropServices.Architecture.Arm ||
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == 
            System.Runtime.InteropServices.Architecture.Arm64)
        {
            return "Production";
        }

        // Check if running on Linux (common for Pi deployments)
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Linux))
        {
            return "Production";
        }

        // Default to Development for Windows/macOS
        return "Development";
    }

    /// <summary>
    /// Check if the local relay server is reachable
    /// </summary>
    private static bool CanReachLocalRelay()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var response = client.GetAsync("http://192.168.1.100:5000/health").Result;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
