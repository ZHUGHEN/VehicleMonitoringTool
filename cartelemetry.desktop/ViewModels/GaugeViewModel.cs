using System;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CarTelemetry.Desktop.ViewModels;

/// <summary>
/// View model for a single configurable dashboard gauge.
/// </summary>
public partial class GaugeViewModel : ObservableObject
{
    
    [ObservableProperty] private string _title = "";
    
    [ObservableProperty] private double _value = 0;
    
    [ObservableProperty] private string _unit = "";
    
    [ObservableProperty] private double _maximum = 100;
    
    [ObservableProperty] private string _color = "#3B82F6";
    
    [ObservableProperty] private GaugeType _gaugeType = GaugeType.None;

    public GaugeViewModel()
    {
    }

    public GaugeViewModel(GaugeType gaugeType)
    {
        GaugeType = gaugeType;
        UpdateGaugeProperties();
    }

    private void UpdateGaugeProperties()
    {
        // Centralize gauge display metadata so the XAML only binds to ready-to-render values.
        switch (GaugeType)
        {
            case GaugeType.EngineRpm:
                Title = "Engine Speed";
                Unit = "RPM";
                Maximum = 8000;
                Color = "#DC2626";
                break;
                
            case GaugeType.EngineLoad:
                Title = "Engine Load";
                Unit = "%";
                Maximum = 100;
                Color = "#F59E0B";
                break;
                
            case GaugeType.CoolantTemperature:
                Title = "Coolant Temperature";
                Unit = "°C";
                Maximum = 120;
                Color = "#EF4444";
                break;
                
            case GaugeType.VehicleSpeed:
                Title = "Vehicle Speed";
                Unit = "MPH";
                Maximum = 200;
                Color = "#3B82F6";
                break;
                
            case GaugeType.FuelPressure:
                Title = "Fuel Pressure";
                Unit = "PSI";
                Maximum = 60;
                Color = "#10B981";
                break;
                
            case GaugeType.FuelTrim:
                Title = "Fuel Trim";
                Unit = "%";
                Maximum = 25;
                Color = "#8B5CF6";
                break;
                
            case GaugeType.None:
            default:
                Title = "No Gauge";
                Unit = "";
                Maximum = 100;
                Color = "#6B7280";
                Value = 0;
                break;
        }
    }

    public void UpdateValue(double rpm, double speedMph, double coolantC, double engineLoad = 0, double fuelPressure = 0, double fuelTrim = 0)
    {
        // Additional values are passed in as the OBD model expands beyond the first three live PIDs.
        Value = GaugeType switch
        {
            GaugeType.EngineRpm => rpm,
            GaugeType.EngineLoad => engineLoad,
            GaugeType.CoolantTemperature => coolantC,
            GaugeType.VehicleSpeed => speedMph,
            GaugeType.FuelPressure => fuelPressure,
            GaugeType.FuelTrim => fuelTrim,
            _ => 0
        };
    }
}


