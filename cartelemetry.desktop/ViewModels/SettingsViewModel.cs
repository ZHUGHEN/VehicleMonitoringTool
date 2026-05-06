using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using CarTelemetry.Desktop.Configuration;
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

    [ObservableProperty] private string _baseUrl = "http://localhost:5000";
    [ObservableProperty] private string _vehicleId = "Z33-01";
    [ObservableProperty] private string _sessionId = "dev-local";
    [ObservableProperty] private string _ingestKey = "super-secret-123";
    [ObservableProperty] private string _diagnosticCacheStatus = "";

    [ObservableProperty] private bool _isModified = false;
    [ObservableProperty] private bool _isTransmitting = false;

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

    public SettingsViewModel(
        IAgentService agentService,
        IDiagnosticAnalysisCacheService diagnosticAnalysisCache)
    {
        _agentService = agentService;
        _diagnosticAnalysisCache = diagnosticAnalysisCache;

        _gaugeSlot1 = AvailableGauges.First(g => g.Type == GaugeType.EngineRpm);
        _gaugeSlot2 = AvailableGauges.First(g => g.Type == GaugeType.EngineLoad);
        _gaugeSlot3 = AvailableGauges.First(g => g.Type == GaugeType.CoolantTemperature);
        _gaugeSlot4 = AvailableGauges.First(g => g.Type == GaugeType.VehicleSpeed);
        _gaugeSlot5 = AvailableGauges.First(g => g.Type == GaugeType.None);
        _gaugeSlot6 = AvailableGauges.First(g => g.Type == GaugeType.None);

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
                e.PropertyName != nameof(DiagnosticCacheStatus))
            {
                IsModified = true;
            }
        };

        // Load persisted settings in the background so opening the window is not blocked by disk I/O.
        _ = Task.Run(LoadGaugeConfigurationAsync);
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
