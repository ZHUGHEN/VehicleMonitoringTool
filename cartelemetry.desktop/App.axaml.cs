using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CarTelemetry.Desktop.ViewModels;
using CarTelemetry.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CarTelemetry.Desktop;

/// <summary>
/// Avalonia application bootstrapper that resolves the main window from the service container.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; set; } = default!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

