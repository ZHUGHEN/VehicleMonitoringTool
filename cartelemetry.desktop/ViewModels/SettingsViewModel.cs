using System.ComponentModel;
using System.Runtime.CompilerServices;
using CarTelemetry.Desktop.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarTelemetry.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private string _baseUrl = "http://localhost:5000";
    [ObservableProperty] private string _vehicleId = "Z33-01";
    [ObservableProperty] private string _sessionId = "dev-local";
    [ObservableProperty] private string _ingestKey = "super-secret-123";
    
    [ObservableProperty] private bool _isModified = false;

    public SettingsViewModel()
    {
        // Monitor for changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(IsModified))
            {
                IsModified = true;
            }
        };
    }

    [RelayCommand]
    private void LoadDefaults()
    {
        BaseUrl = "http://localhost:5000";
        VehicleId = "Z33-01";
        SessionId = "dev-local";
        IngestKey = "super-secret-123";
    }

    [RelayCommand]
    private void LoadProductionDefaults()
    {
        // Example production settings - you'll customize these
        BaseUrl = "http://192.168.1.100:5000";  // Your desktop IP
        VehicleId = "Z33-01";
        SessionId = "production";
        IngestKey = "your-production-key";
    }

    [RelayCommand]
    private void Save()
    {
        // TODO: Implement saving to configuration file
        IsModified = false;
    }

    public RelayConfiguration ToConfiguration()
    {
        return new RelayConfiguration
        {
            BaseUrl = BaseUrl,
            VehicleId = VehicleId,
            SessionId = SessionId,
            IngestKey = IngestKey
        };
    }

    public void LoadFromConfiguration(RelayConfiguration config)
    {
        BaseUrl = config.BaseUrl;
        VehicleId = config.VehicleId;
        SessionId = config.SessionId;
        IngestKey = config.IngestKey;
        IsModified = false;
    }
}