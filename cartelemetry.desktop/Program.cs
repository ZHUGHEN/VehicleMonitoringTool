using Avalonia;
using System;
using CarTelemetry.Core;               // <-- add this
using CarTelemetry.Core.Obd;
using Microsoft.Extensions.DependencyInjection; // <-- add this

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
            .AddSingleton<IObdAdapter, MockObdAdapter>()   // swap to Elm327SerialAdapter later
            .AddSingleton<IObdPoller, ObdPoller>()
            .AddSingleton<MainViewModel>()
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
