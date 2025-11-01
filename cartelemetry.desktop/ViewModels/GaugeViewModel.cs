using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CarTelemetry.Desktop.ViewModels;

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
        switch (GaugeType)
        {
            case GaugeType.EngineRpm:
                Title = "Engine Speed";
                Unit = "RPM";
                Maximum = 8000;
                Color = "#DC2626"; // Red for RPM
                break;
                
            case GaugeType.EngineLoad:
                Title = "Engine Load";
                Unit = "%";
                Maximum = 100;
                Color = "#F59E0B"; // Yellow for load
                break;
                
            case GaugeType.CoolantTemperature:
                Title = "Coolant Temperature";
                Unit = "°C";
                Maximum = 120;
                Color = "#EF4444"; // Red for temperature
                break;
                
            case GaugeType.VehicleSpeed:
                Title = "Vehicle Speed";
                Unit = "MPH";
                Maximum = 200;
                Color = "#3B82F6"; // Blue for speed
                break;
                
            case GaugeType.FuelPressure:
                Title = "Fuel Pressure";
                Unit = "PSI";
                Maximum = 60;
                Color = "#10B981"; // Green for fuel
                break;
                
            case GaugeType.FuelTrim:
                Title = "Fuel Trim";
                Unit = "%";
                Maximum = 25;
                Color = "#8B5CF6"; // Purple for fuel trim
                break;
                
            case GaugeType.None:
            default:
                Title = "No Gauge";
                Unit = "";
                Maximum = 100;
                Color = "#6B7280"; // Gray for none
                Value = 0;
                break;
        }
    }

    public void UpdateValue(double rpm, double speedMph, double coolantC, double engineLoad = 0, double fuelPressure = 0, double fuelTrim = 0)
    {
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