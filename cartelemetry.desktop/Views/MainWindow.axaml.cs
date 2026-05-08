using Avalonia.Controls;
using CarTelemetry.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace CarTelemetry.Desktop.Views;

/// <summary>
/// Main Avalonia window that wires dashboard, settings, and diagnostics views.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _mainViewModel;
    private SettingsView? _settingsView;
    private SettingsViewModel? _settingsViewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        this.Loaded += (s, e) =>
        {
            // Resolve view models after Avalonia has created named controls in the visual tree.
            _mainViewModel = App.Services.GetRequiredService<MainViewModel>();
            _settingsViewModel = App.Services.GetRequiredService<SettingsViewModel>();
            this.DataContext = _mainViewModel;
            
            // Diagnostics owns a separate ViewModel, so assign it explicitly after the tab is created.
            if (this.FindControl<DiagnosticsView>("DiagnosticsView") is DiagnosticsView diagnosticsView)
            {
                diagnosticsView.DataContext = App.Services.GetRequiredService<DiagnosticsViewModel>();
            }

            if (this.FindControl<ServiceRecordsView>("ServiceRecordsView") is ServiceRecordsView serviceRecordsView)
            {
                serviceRecordsView.DataContext = App.Services.GetRequiredService<ServiceRecordsViewModel>();
            }
            
            _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
            
            _settingsViewModel.PropertyChanged += OnSettingsViewModelPropertyChanged;
        };
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsShowingSettings))
        {
            var telemetryView = this.FindControl<Grid>("TelemetryView");
            var settingsView = this.FindControl<Grid>("SettingsView");
            var settingsContent = this.FindControl<ContentControl>("SettingsContent");
            
            if (_mainViewModel?.IsShowingSettings == true)
            {
                // Lazily create the settings view so the dashboard starts quickly and keeps one settings instance.
                if (_settingsView == null)
                {
                    _settingsView = new SettingsView();
                    _settingsView.DataContext = _settingsViewModel;
                }
                
                settingsContent!.Content = _settingsView;
                telemetryView!.IsVisible = false;
                settingsView!.IsVisible = true;
            }
            else
            {
                telemetryView!.IsVisible = true;
                settingsView!.IsVisible = false;
            }
        }
    }

    private async void OnSettingsViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.IsModified) && 
            _settingsViewModel?.IsModified == false && 
            _mainViewModel != null)
        {
            // A transition back to "not modified" means settings were saved and the dashboard should reload layout.
            await _mainViewModel.RefreshGaugeConfigurationAsync();
        }
    }
}

