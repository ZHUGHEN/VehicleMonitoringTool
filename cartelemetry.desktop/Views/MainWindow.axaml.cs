using Avalonia.Controls;
using CarTelemetry.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace CarTelemetry.Desktop.Views;

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
            // Set up DataContexts
            _mainViewModel = App.Services.GetRequiredService<MainViewModel>();
            _settingsViewModel = App.Services.GetRequiredService<SettingsViewModel>();
            this.DataContext = _mainViewModel;
            
            if (this.FindControl<DiagnosticsView>("DiagnosticsView") is DiagnosticsView diagnosticsView)
            {
                diagnosticsView.DataContext = App.Services.GetRequiredService<DiagnosticsViewModel>();
            }
            
            // Handle navigation
            _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
            
            // Listen for settings changes to refresh gauge configuration
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
                // Create settings view if it doesn't exist
                if (_settingsView == null)
                {
                    _settingsView = new SettingsView();
                    _settingsView.DataContext = _settingsViewModel;
                }
                
                // Set content and show settings
                settingsContent!.Content = _settingsView;
                telemetryView!.IsVisible = false;
                settingsView!.IsVisible = true;
            }
            else
            {
                // Show telemetry, hide settings
                telemetryView!.IsVisible = true;
                settingsView!.IsVisible = false;
            }
        }
    }

    private async void OnSettingsViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // When IsModified changes from true to false, it means settings were saved
        if (e.PropertyName == nameof(SettingsViewModel.IsModified) && 
            _settingsViewModel?.IsModified == false && 
            _mainViewModel != null)
        {
            // Refresh gauge configuration in MainViewModel
            await _mainViewModel.RefreshGaugeConfigurationAsync();
        }
    }
}