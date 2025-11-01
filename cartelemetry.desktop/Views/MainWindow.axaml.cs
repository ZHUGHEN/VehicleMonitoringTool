using Avalonia.Controls;
using CarTelemetry.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CarTelemetry.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Set up the DiagnosticsView DataContext when the window is loaded
        this.Loaded += (s, e) =>
        {
            if (this.FindControl<DiagnosticsView>("DiagnosticsView") is DiagnosticsView diagnosticsView)
            {
                diagnosticsView.DataContext = App.Services.GetRequiredService<DiagnosticsViewModel>();
            }
        };
    }
}