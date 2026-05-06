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
        var app = BuildAvaloniaApp();

        // The desktop app defaults to production-style settings on Linux/ARM targets such as Raspberry Pi.
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                            ?? DetectEnvironment();
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .Build();

        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().AddConfiguration(configuration.GetSection("Logging")));
        var startupLogger = loggerFactory.CreateLogger("Startup");
        
        startupLogger.LogInformation("🔧 Running in {Environment} environment", environmentName);
        
        System.Diagnostics.Debug.WriteLine($"🔧 CarTelemetry: Running in {environmentName} environment");

        var relayConfig = new RelayConfiguration();
        configuration.GetSection("Relay").Bind(relayConfig);

        var openAiConfig = new OpenAiConfiguration();
        configuration.GetSection("OpenAI").Bind(openAiConfig);

        var obdConfig = new ObdConfiguration();
        configuration.GetSection("Obd").Bind(obdConfig);

        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole().AddConfiguration(configuration.GetSection("Logging")))
            
            // OBD_PORT takes precedence so hardware can be selected without editing appsettings files.
            .AddSingleton<IObdAdapter>(_ =>
            {
                var envPortName = Environment.GetEnvironmentVariable("OBD_PORT");
                var portName = envPortName ?? obdConfig.PortName;
                var useMock = string.IsNullOrWhiteSpace(envPortName)
                    && (obdConfig.UseMock || string.IsNullOrWhiteSpace(portName));

                return useMock
                    ? new MockObdAdapter()
                    : new Elm327Adapter(portName!, obdConfig.Baud);
            })
            
            .AddSingleton<IObdPoller, ObdPoller>()
            .AddSingleton<IDtcDescriptionService, DtcDescriptionService>()
            .AddSingleton<IDtcService, DtcService>()
            
            .AddSingleton<ITelemetryPublisher>(_ => 
            {
                // Publisher identity is attached to every relay envelope for SignalR grouping.
                var baseUrl = new Uri(relayConfig.BaseUrl);
                return new RelayPublisher(baseUrl, relayConfig.VehicleId, relayConfig.SessionId, relayConfig.IngestKey);
            })
            .AddSingleton<IAgentService, AgentService>()
            .AddSingleton(openAiConfig)
            .AddSingleton<IDiagnosticAnalysisCacheService, JsonDiagnosticAnalysisCacheService>()
            .AddSingleton<IDiagnosticAiService, OpenAiDiagnosticService>()
            
            .AddSingleton<MainViewModel>()
            .AddSingleton<DiagnosticsViewModel>()
            .AddSingleton<SettingsViewModel>()
            .BuildServiceProvider();

        App.Services = services;

        app.StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static string DetectEnvironment()
    {
        var manualEnv = Environment.GetEnvironmentVariable("Z33_ENVIRONMENT");
        if (!string.IsNullOrEmpty(manualEnv))
        {
            return manualEnv;
        }

        if (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == 
            System.Runtime.InteropServices.Architecture.Arm ||
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == 
            System.Runtime.InteropServices.Architecture.Arm64)
        {
            return "Production";
        }

        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Linux))
        {
            return "Production";
        }

        return "Development";
    }
}


