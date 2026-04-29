// Essential Avalonia UI imports - Avalonia is the cross-platform UI framework (like WPF but for macOS/Linux too)
using Avalonia;
using System;
// Core business logic layer - contains OBD-II communication, telemetry data models, and hardware interfaces
using CarTelemetry.Core;               
using CarTelemetry.Core.Obd;
// Desktop application layer - ViewModels (MVVM pattern), Services (business logic), and Configuration
using CarTelemetry.Desktop.ViewModels;
using CarTelemetry.Desktop.Services;
using CarTelemetry.Desktop.Configuration;
// Microsoft dependency injection - allows us to wire up services and make them available throughout the app
using Microsoft.Extensions.DependencyInjection;
// Logging framework - provides structured logging with different levels (Debug, Info, Warning, Error)
using Microsoft.Extensions.Logging;
// Configuration framework - loads settings from appsettings.json files based on environment
using Microsoft.Extensions.Configuration;

namespace CarTelemetry.Desktop;

/// <summary>
/// Main entry point for the CarTelemetry Desktop application.
/// This class is responsible for:
/// 1. Setting up the Avalonia UI framework
/// 2. Configuring dependency injection (DI) container with all services
/// 3. Loading configuration based on environment (Development/Production)
/// 4. Initializing logging system
/// 5. Starting the application
/// 
/// Think of this as the "bootstrap" or "startup" code that runs before any UI appears.
/// </summary>
class Program
{
    // [STAThread] configures Windows UI threading model - required for proper Windows behavior
    // This attribute is safely ignored on Linux/macOS/ARM where STA threading doesn't apply
    // Avalonia framework expects this attribute for cross-platform compatibility
    [STAThread]
    public static void Main(string[] args)
    {
        // Step 1: Create the Avalonia application builder (but don't start it yet)
        // This configures the UI framework with platform detection, fonts, etc.
        var app = BuildAvaloniaApp();

        // Step 2: Smart environment detection
        // Determines if we're running in Development (Windows/macOS dev machine) or Production (Raspberry Pi in car)
        // Checks ASPNETCORE_ENVIRONMENT first, then falls back to hardware/OS detection
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                            ?? DetectEnvironment();
        
        // Step 3: Build configuration system
        // Loads appsettings.json (base settings) + appsettings.{Environment}.json (environment-specific overrides)
        // This gives us things like relay server URLs, logging levels, vehicle IDs, etc.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)  // Look in the app's directory
            .AddJsonFile("appsettings.json", optional: false)    // Base settings (required)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)  // Environment overrides (optional)
            .Build();

        // Step 4: Create early logger for startup diagnostics
        // This temporary logger helps us debug configuration and startup issues
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().AddConfiguration(configuration.GetSection("Logging")));
        var startupLogger = loggerFactory.CreateLogger("Startup");
        
        // Log which environment we detected/are using
        startupLogger.LogInformation("🔧 Running in {Environment} environment", environmentName);
        
        // Also show in Visual Studio debug output window during development
        System.Diagnostics.Debug.WriteLine($"🔧 CarTelemetry: Running in {environmentName} environment");

        // Step 5: Load relay configuration
        // The relay is the web server that receives telemetry data from this desktop app
        // RelayConfiguration maps to the "Relay" section in appsettings.json
        var relayConfig = new RelayConfiguration();
        configuration.GetSection("Relay").Bind(relayConfig);

        var openAiConfig = new OpenAiConfiguration();
        configuration.GetSection("OpenAI").Bind(openAiConfig);

        var obdConfig = new ObdConfiguration();
        configuration.GetSection("Obd").Bind(obdConfig);

        // Step 6: Dependency Injection (DI) Setup
        // This is where we wire up all the services that the app needs
        // Services are created once (Singleton) and injected wherever needed
        var services = new ServiceCollection()
            // Logging: Available throughout the app for debugging and monitoring
            .AddLogging(builder => builder.AddConsole().AddConfiguration(configuration.GetSection("Logging")))
            
            // OBD-II Hardware Interface: uses mock data unless an ELM327 port is configured
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
            
            // OBD Polling Service: Continuously requests data from the OBD adapter
            .AddSingleton<IObdPoller, ObdPoller>()
            // Diagnostic Trouble Code (DTC) Services: For reading and interpreting car error codes
            .AddSingleton<IDtcDescriptionService, DtcDescriptionService>() // Looks up human-readable descriptions for error codes
            .AddSingleton<IDtcService, DtcService>()       // Handles DTC reading, parsing, and management
            
            // Telemetry Transmission Services: For sending data to the web relay/dashboard
            .AddSingleton<ITelemetryPublisher>(_ => 
            {
                // Create the relay publisher with configuration from appsettings.json
                // This handles HTTP POST requests to send telemetry data to the web server
                var baseUrl = new Uri(relayConfig.BaseUrl);
                return new RelayPublisher(baseUrl, relayConfig.VehicleId, relayConfig.SessionId, relayConfig.IngestKey);
            })
            .AddSingleton<IAgentService, AgentService>()   // Controllable agent service for managing data transmission
            .AddSingleton(openAiConfig)
            .AddSingleton<IDiagnosticAiService, OpenAiDiagnosticService>()
            
            // ViewModels: The "VM" in MVVM pattern - these contain the business logic and data for each UI view
            .AddSingleton<MainViewModel>()                 // Main dashboard with gauges, lap timer, screensaver
            .AddSingleton<DiagnosticsViewModel>()          // Diagnostics tab with DTC codes and car health
            .AddSingleton<SettingsViewModel>()             // Settings overlay for gauge configuration
            .BuildServiceProvider();                       // Create the actual DI container

        // Step 7: Make the DI container available to the Avalonia app
        // App.Services is used by App.axaml.cs to resolve ViewModels and inject them into Views
        App.Services = services; // makes DI container accessible to App.axaml.cs
        // ---------------------------------------------------

        // Step 8: Start the application!
        // This creates the main window, shows the UI, and enters the event loop
        app.StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Configures the Avalonia UI framework with platform-specific settings.
    /// This method sets up:
    /// - Platform detection (Windows, macOS, Linux)
    /// - Inter font family (modern, clean font)
    /// - Debug tracing for UI framework issues
    /// </summary>
    /// <returns>Configured AppBuilder ready to start</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()           // Use our App class from App.axaml.cs
            .UsePlatformDetect()                 // Automatically detect Windows/macOS/Linux and use appropriate backends
            .WithInterFont()                     // Use Inter font family (clean, modern, great for automotive UIs)
            .LogToTrace();                       // Send Avalonia internal logs to System.Diagnostics.Trace

    /// <summary>
    /// Smart environment detection based on platform and hardware.
    /// This determines if we're running in a development environment (Windows/macOS dev machine)
    /// or production environment (Raspberry Pi in the car).
    /// 
    /// Detection Logic:
    /// 1. Check Z33_ENVIRONMENT variable (manual override)
    /// 2. Check if running on ARM architecture (Raspberry Pi)
    /// 3. Check if running on Linux (common for Pi deployments)
    /// 4. Default to Development for Windows/macOS
    /// 
    /// Why this matters:
    /// - Development: Use mock OBD data, local relay server, debug logging
    /// - Production: Use real ELM327 hardware, production relay, minimal logging
    /// </summary>
    /// <returns>Environment name: "Development" or "Production"</returns>
    private static string DetectEnvironment()
    {
        // Check for manual environment override first
        // Allows developers to force Production mode for testing, or Pi to use Development mode for debugging
        var manualEnv = Environment.GetEnvironmentVariable("Z33_ENVIRONMENT");
        if (!string.IsNullOrEmpty(manualEnv))
        {
            // Note: Can't use logger here as it's not created yet, but this will show in logs later
            return manualEnv;
        }

        // Check if running on ARM architecture (Raspberry Pi, etc.)
        // ARM processors are typically used in embedded automotive systems
        if (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == 
            System.Runtime.InteropServices.Architecture.Arm ||
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == 
            System.Runtime.InteropServices.Architecture.Arm64)
        {
            return "Production";
        }

        // Check if running on Linux (common for Pi deployments)
        // Most automotive embedded systems run Linux for cost and customization
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Linux))
        {
            return "Production";
        }

        // Default to Development for Windows/macOS
        // These are typically developer workstations
        return "Development";
    }
}
