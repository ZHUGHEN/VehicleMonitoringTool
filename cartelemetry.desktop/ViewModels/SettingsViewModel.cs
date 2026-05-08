using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CarTelemetry.Desktop.Configuration;
using CarTelemetry.Desktop.Models;
using CarTelemetry.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarTelemetry.Desktop.ViewModels;

/// <summary>
/// Supported gauge slots for the dashboard configuration UI.
/// </summary>
public enum GaugeType
{
    None,
    EngineRpm,
    EngineLoad,
    CoolantTemperature,
    VehicleSpeed,
    FuelPressure,
    FuelTrim
}

/// <summary>
/// Display option for a selectable gauge slot.
/// </summary>
public record GaugeOption(GaugeType Type, string Icon, string DisplayName)
{
    public string DisplayText => $"{Icon} {DisplayName}";
}

/// <summary>
/// Manages relay settings, gauge layout persistence, and dashboard transmission controls.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IAgentService _agentService;
    private readonly IDiagnosticAnalysisCacheService _diagnosticAnalysisCache;
    private readonly IVehicleSetupStore _vehicleSetupStore;
    private readonly IVehicleCatalogService _vehicleCatalog;
    private bool _isLoadingVehicleSetup;

    [ObservableProperty] private string _baseUrl = "http://localhost:5000";
    [ObservableProperty] private string _vehicleId = "Z33-01";
    [ObservableProperty] private string _sessionId = "dev-local";
    [ObservableProperty] private string _ingestKey = "super-secret-123";
    [ObservableProperty] private string _diagnosticCacheStatus = "";

    [ObservableProperty] private bool _isModified = false;
    [ObservableProperty] private bool _isTransmitting = false;
    [ObservableProperty] private int _selectedVehicleYear;
    [ObservableProperty] private string? _selectedVehicleMake;
    [ObservableProperty] private string? _selectedVehicleModel;
    [ObservableProperty] private bool _isLoadingVehicleMakes;
    [ObservableProperty] private bool _isLoadingVehicleModels;
    [ObservableProperty] private string _vehicleSetupStatus = "";

    [ObservableProperty] private GaugeOption _gaugeSlot1;
    [ObservableProperty] private GaugeOption _gaugeSlot2;
    [ObservableProperty] private GaugeOption _gaugeSlot3;
    [ObservableProperty] private GaugeOption _gaugeSlot4;
    [ObservableProperty] private GaugeOption _gaugeSlot5;
    [ObservableProperty] private GaugeOption _gaugeSlot6;

    public List<GaugeOption> AvailableGauges { get; } = new()
    {
        new GaugeOption(GaugeType.None, "🚫", "None"),
        new GaugeOption(GaugeType.EngineRpm, "🔄", "Engine RPM"),
        new GaugeOption(GaugeType.EngineLoad, "⚡", "Engine Load"),
        new GaugeOption(GaugeType.CoolantTemperature, "🌡️", "Coolant Temperature"),
        new GaugeOption(GaugeType.VehicleSpeed, "🏎️", "Vehicle Speed"),
        new GaugeOption(GaugeType.FuelPressure, "⛽", "Fuel Pressure"),
        new GaugeOption(GaugeType.FuelTrim, "🔧", "Fuel Trim")
    };

    public List<int> VehicleYears { get; } = Enumerable.Range(1996, DateTime.Now.Year - 1995)
        .Reverse()
        .ToList();

    public ObservableCollection<string> VehicleMakes { get; } = new();

    public ObservableCollection<string> VehicleModels { get; } = new();

    public SettingsViewModel(
        IAgentService agentService,
        IDiagnosticAnalysisCacheService diagnosticAnalysisCache,
        IVehicleSetupStore vehicleSetupStore,
        IVehicleCatalogService vehicleCatalog)
    {
        _agentService = agentService;
        _diagnosticAnalysisCache = diagnosticAnalysisCache;
        _vehicleSetupStore = vehicleSetupStore;
        _vehicleCatalog = vehicleCatalog;

        _gaugeSlot1 = AvailableGauges.First(g => g.Type == GaugeType.EngineRpm);
        _gaugeSlot2 = AvailableGauges.First(g => g.Type == GaugeType.EngineLoad);
        _gaugeSlot3 = AvailableGauges.First(g => g.Type == GaugeType.CoolantTemperature);
        _gaugeSlot4 = AvailableGauges.First(g => g.Type == GaugeType.VehicleSpeed);
        _gaugeSlot5 = AvailableGauges.First(g => g.Type == GaugeType.None);
        _gaugeSlot6 = AvailableGauges.First(g => g.Type == GaugeType.None);
        _selectedVehicleYear = DateTime.Now.Year;

        // Mirror the agent state so the settings toggle stays in sync with the dashboard header.
        _agentService.TransmissionStateChanged += (s, isTransmitting) =>
        {
            IsTransmitting = isTransmitting;
        };

        // Most editable settings should enable Save; status-only properties are intentionally ignored.
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(IsModified) &&
                e.PropertyName != nameof(IsTransmitting) &&
                e.PropertyName != nameof(DiagnosticCacheStatus) &&
                e.PropertyName != nameof(IsLoadingVehicleMakes) &&
                e.PropertyName != nameof(IsLoadingVehicleModels) &&
                e.PropertyName != nameof(VehicleSetupStatus) &&
                !_isLoadingVehicleSetup)
            {
                IsModified = true;
            }
        };

        // Load persisted settings in the background so opening the window is not blocked by disk I/O.
        _ = Task.Run(LoadGaugeConfigurationAsync);
        _ = Task.Run(LoadVehicleSetupAsync);
    }

    partial void OnSelectedVehicleYearChanged(int value)
    {
        if (!_isLoadingVehicleSetup)
        {
            _ = Task.Run(() => LoadVehicleModelsAsync());
        }
    }

    partial void OnSelectedVehicleMakeChanged(string? value)
    {
        if (!_isLoadingVehicleSetup)
        {
            _ = Task.Run(() => LoadVehicleModelsAsync());
        }
    }

    [RelayCommand]
    public async Task ToggleTransmissionAsync()
    {
        if (IsTransmitting)
        {
            await _agentService.StopTransmissionAsync();
        }
        else
        {
            await _agentService.StartTransmissionAsync();
        }
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
        BaseUrl = "http://192.168.1.100:5000";
        VehicleId = "Z33-01";
        SessionId = "production";
        IngestKey = "your-production-key";
    }

    [RelayCommand]
    private async Task ClearDiagnosticCacheAsync()
    {
        try
        {
            await _diagnosticAnalysisCache.ClearAsync(default);
            DiagnosticCacheStatus = "Diagnostic AI cache cleared.";
        }
        catch (Exception ex)
        {
            DiagnosticCacheStatus = $"Failed to clear diagnostic AI cache: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            // Gauge layout is persisted independently from relay defaults for now.
            var gaugeConfig = GetCurrentGaugeConfiguration();
            await SaveGaugeConfigurationAsync(gaugeConfig);
            await SaveVehicleSetupAsync();

            IsModified = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    private GaugeConfiguration GetCurrentGaugeConfiguration()
    {
        return new GaugeConfiguration
        {
            GaugeSlot1 = GaugeSlot1?.Type ?? GaugeType.None,
            GaugeSlot2 = GaugeSlot2?.Type ?? GaugeType.None,
            GaugeSlot3 = GaugeSlot3?.Type ?? GaugeType.None,
            GaugeSlot4 = GaugeSlot4?.Type ?? GaugeType.None,
            GaugeSlot5 = GaugeSlot5?.Type ?? GaugeType.None,
            GaugeSlot6 = GaugeSlot6?.Type ?? GaugeType.None
        };
    }

    private async Task SaveGaugeConfigurationAsync(GaugeConfiguration config)
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CarTelemetry");
        Directory.CreateDirectory(appDataPath);

        var filePath = Path.Combine(appDataPath, "gauge-config.json");
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task LoadGaugeConfigurationAsync()
    {
        try
        {
            // Keep user-specific layout outside the repo so development and production builds share code.
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CarTelemetry");
            var filePath = Path.Combine(appDataPath, "gauge-config.json");

            if (!File.Exists(filePath))
            {
                var defaultConfig = GaugeConfiguration.CreateDefault();
                LoadGaugeConfiguration(defaultConfig);
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<GaugeConfiguration>(json);

            if (config != null)
            {
                LoadGaugeConfiguration(config);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load gauge configuration: {ex.Message}");
            LoadGaugeConfiguration(GaugeConfiguration.CreateDefault());
        }
    }

    private void LoadGaugeConfiguration(GaugeConfiguration config)
    {
        GaugeSlot1 = AvailableGauges.First(g => g.Type == config.GaugeSlot1);
        GaugeSlot2 = AvailableGauges.First(g => g.Type == config.GaugeSlot2);
        GaugeSlot3 = AvailableGauges.First(g => g.Type == config.GaugeSlot3);
        GaugeSlot4 = AvailableGauges.First(g => g.Type == config.GaugeSlot4);
        GaugeSlot5 = AvailableGauges.First(g => g.Type == config.GaugeSlot5);
        GaugeSlot6 = AvailableGauges.First(g => g.Type == config.GaugeSlot6);
    }

    private async Task LoadVehicleSetupAsync()
    {
        _isLoadingVehicleSetup = true;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoadingVehicleMakes = true;
                VehicleSetupStatus = "Loading vehicle makes...";
            });

            var makes = await _vehicleCatalog.GetMakesAsync();
            var setup = await _vehicleSetupStore.LoadAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                VehicleMakes.Clear();

                foreach (var make in makes)
                {
                    VehicleMakes.Add(make);
                }

                SelectedVehicleYear = setup?.Year >= 1996
                    ? setup.Year
                    : DateTime.Now.Year;
                SelectedVehicleMake = !string.IsNullOrWhiteSpace(setup?.Make) && VehicleMakes.Contains(setup.Make)
                    ? setup.Make
                    : VehicleMakes.FirstOrDefault();
                SelectedVehicleModel = setup?.Model;
                IsLoadingVehicleMakes = false;
            });

            await LoadVehicleModelsAsync(setup?.Model);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoadingVehicleMakes = false;
                VehicleSetupStatus = $"Vehicle catalog unavailable: {ex.Message}";
            });
        }
        finally
        {
            _isLoadingVehicleSetup = false;
        }
    }

    private async Task LoadVehicleModelsAsync(string? preferredModel = null)
    {
        var year = SelectedVehicleYear;
        var make = SelectedVehicleMake;

        if (year < 1996 || string.IsNullOrWhiteSpace(make))
        {
            return;
        }

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoadingVehicleModels = true;
                VehicleSetupStatus = "Loading vehicle models...";
            });

            var models = await _vehicleCatalog.GetModelsAsync(year, make);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                VehicleModels.Clear();

                foreach (var model in models)
                {
                    VehicleModels.Add(model);
                }

                if (!string.IsNullOrWhiteSpace(preferredModel) && VehicleModels.Contains(preferredModel))
                {
                    SelectedVehicleModel = preferredModel;
                }
                else
                {
                    SelectedVehicleModel = VehicleModels.FirstOrDefault();
                }

                IsLoadingVehicleModels = false;
                VehicleSetupStatus = VehicleModels.Count > 0
                    ? "Vehicle catalog loaded."
                    : "No models found for that year and make.";
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoadingVehicleModels = false;
                VehicleSetupStatus = $"Vehicle models unavailable: {ex.Message}";
            });
        }
    }

    private async Task SaveVehicleSetupAsync()
    {
        var setup = new VehicleSetup
        {
            Year = SelectedVehicleYear,
            Make = SelectedVehicleMake ?? "",
            Model = SelectedVehicleModel ?? ""
        };

        await _vehicleSetupStore.SaveAsync(setup);
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
