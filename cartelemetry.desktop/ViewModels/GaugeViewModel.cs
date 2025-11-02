// System imports
using System;

// MVVM Community Toolkit import for observable properties
using CommunityToolkit.Mvvm.ComponentModel; // Provides [ObservableProperty] attribute for automatic property notifications

namespace CarTelemetry.Desktop.ViewModels;

/// <summary>
/// GaugeViewModel represents a single gauge display in the automotive dashboard.
/// This is a specialized ViewModel that encapsulates all the data and behavior needed to display one gauge.
/// 
/// Key Responsibilities:
/// 1. Storing gauge configuration (title, units, maximum value, color)
/// 2. Holding the current display value
/// 3. Providing automatic property change notifications for UI binding
/// 4. Mapping telemetry data to the appropriate gauge value based on gauge type
/// 5. Managing gauge appearance and behavior based on the selected gauge type
/// 
/// MVVM Pattern Usage:
/// - This ViewModel represents the data and logic for a single gauge control
/// - The View (GaugeControl.axaml) binds to these properties to display the gauge
/// - ObservableObject base class provides INotifyPropertyChanged implementation
/// - [ObservableProperty] attributes automatically generate properties with change notifications
/// 
/// Design Pattern: Strategy Pattern
/// The GaugeType enum acts as a strategy selector, and UpdateValue() method implements
/// different strategies for mapping telemetry data to display values based on the gauge type.
/// </summary>
public partial class GaugeViewModel : ObservableObject
{
    // ===== OBSERVABLE PROPERTIES =====
    // These properties use the [ObservableProperty] attribute from CommunityToolkit.Mvvm
    // This automatically generates:
    // 1. Public properties with proper getter/setter
    // 2. PropertyChanged event notifications when values change
    // 3. Backing fields with underscore prefix (e.g., _title, _value)
    
    /// <summary>Display title for the gauge (e.g., "Engine Speed", "Coolant Temperature")</summary>
    [ObservableProperty] private string _title = "";
    
    /// <summary>Current numeric value to display on the gauge (e.g., 3500 RPM, 85°C)</summary>
    [ObservableProperty] private double _value = 0;
    
    /// <summary>Unit of measurement for the gauge (e.g., "RPM", "°C", "MPH", "%")</summary>
    [ObservableProperty] private string _unit = "";
    
    /// <summary>Maximum value for the gauge scale (determines gauge arc range)</summary>
    [ObservableProperty] private double _maximum = 100;
    
    /// <summary>Color for the gauge display (hex color code, e.g., "#DC2626" for red)</summary>
    [ObservableProperty] private string _color = "#3B82F6";
    
    /// <summary>Type of gauge this ViewModel represents (determines data source and appearance)</summary>
    [ObservableProperty] private GaugeType _gaugeType = GaugeType.None;

    /// <summary>
    /// Default constructor for design-time support and basic initialization.
    /// Creates a gauge with default values (typically used in XAML previews).
    /// </summary>
    public GaugeViewModel()
    {
    }

    /// <summary>
    /// Primary constructor that creates a gauge configured for a specific type.
    /// This is called by MainViewModel when creating the 6 gauge instances.
    /// </summary>
    /// <param name="gaugeType">The type of gauge to create (RPM, Speed, Temperature, etc.)</param>
    public GaugeViewModel(GaugeType gaugeType)
    {
        GaugeType = gaugeType;
        UpdateGaugeProperties(); // Configure appearance based on the gauge type
    }

    /// <summary>
    /// Configures the gauge appearance and behavior based on its type.
    /// This method implements the Strategy pattern - different gauge types have different:
    /// - Display titles and units
    /// - Maximum scale values (RPM goes to 8000, Speed to 200, etc.)
    /// - Colors for easy visual identification
    /// 
    /// Each gauge type is designed with automotive standards in mind:
    /// - RPM: Red color (danger zone at high RPM), 8000 max (typical sport car redline)
    /// - Speed: Blue color (neutral), 200 MPH max (covers most scenarios)
    /// - Temperature: Red color (overheating danger), 120°C max (typical danger zone)
    /// - Load: Yellow/amber color (caution), 100% max (percentage-based)
    /// - Fuel systems: Green/purple colors (operational), appropriate ranges
    /// </summary>
    private void UpdateGaugeProperties()
    {
        switch (GaugeType)
        {
            case GaugeType.EngineRpm:
                Title = "Engine Speed";        // User-friendly name
                Unit = "RPM";                  // Revolutions Per Minute
                Maximum = 8000;                // Typical redline for sport cars
                Color = "#DC2626";             // Red - indicates potential danger at high values
                break;
                
            case GaugeType.EngineLoad:
                Title = "Engine Load";         // How hard the engine is working
                Unit = "%";                    // Percentage of maximum load
                Maximum = 100;                 // 100% is maximum possible load
                Color = "#F59E0B";             // Yellow/amber - caution color for load monitoring
                break;
                
            case GaugeType.CoolantTemperature:
                Title = "Coolant Temperature"; // Engine cooling system temperature
                Unit = "°C";                   // Celsius (more common in automotive)
                Maximum = 120;                 // Danger zone starts around 100-110°C
                Color = "#EF4444";             // Red - overheating is dangerous
                break;
                
            case GaugeType.VehicleSpeed:
                Title = "Vehicle Speed";       // How fast the vehicle is moving
                Unit = "MPH";                  // Miles Per Hour (converted from km/h)
                Maximum = 200;                 // Covers most practical speed ranges
                Color = "#3B82F6";             // Blue - neutral operational parameter
                break;
                
            case GaugeType.FuelPressure:
                Title = "Fuel Pressure";       // Fuel system pressure
                Unit = "PSI";                  // Pounds per Square Inch
                Maximum = 60;                  // Typical fuel pressure range for most cars
                Color = "#10B981";             // Green - fuel system operational parameter
                break;
                
            case GaugeType.FuelTrim:
                Title = "Fuel Trim";           // Engine fuel mixture adjustment
                Unit = "%";                    // Percentage adjustment from baseline
                Maximum = 25;                  // ±25% is typical range for fuel trim
                Color = "#8B5CF6";             // Purple - specialized diagnostic parameter
                break;
                
            case GaugeType.None:
            default:
                Title = "No Gauge";            // Placeholder when no gauge type is selected
                Unit = "";                     // No units for empty gauge
                Maximum = 100;                 // Default scale
                Color = "#6B7280";             // Gray - indicates inactive/disabled state
                Value = 0;                     // Always show zero for empty gauge
                break;
        }
    }

    /// <summary>
    /// Updates the gauge's display value based on incoming telemetry data.
    /// This method is called from MainViewModel every time new telemetry data arrives (~10 Hz).
    /// 
    /// Key Design Points:
    /// 1. Uses pattern matching (switch expression) for clean, efficient mapping
    /// 2. Each gauge type extracts its relevant value from the telemetry data
    /// 3. Some parameters (engineLoad, fuelPressure, fuelTrim) are currently mock data
    /// 4. Real OBD-II implementation would provide all these values from the car
    /// 5. Setting the Value property automatically triggers UI updates via data binding
    /// 
    /// Data Flow:
    /// Car/Mock → OBD Poller → MainViewModel → GaugeViewModel.UpdateValue() → UI Gauge Control
    /// 
    /// Performance Note:
    /// This method is called frequently (10 times per second), so it uses efficient
    /// pattern matching rather than if/else chains for better performance.
    /// </summary>
    /// <param name="rpm">Engine RPM from OBD-II</param>
    /// <param name="speedMph">Vehicle speed in MPH (converted from km/h)</param>
    /// <param name="coolantC">Engine coolant temperature in Celsius</param>
    /// <param name="engineLoad">Engine load percentage (mock data for now)</param>
    /// <param name="fuelPressure">Fuel pressure in PSI (mock data for now)</param>
    /// <param name="fuelTrim">Fuel trim percentage (mock data for now)</param>
    public void UpdateValue(double rpm, double speedMph, double coolantC, double engineLoad = 0, double fuelPressure = 0, double fuelTrim = 0)
    {
        // Use switch expression for efficient value mapping based on gauge type
        // The Value property setter automatically triggers PropertyChanged event
        Value = GaugeType switch
        {
            GaugeType.EngineRpm => rpm,                    // Direct mapping: RPM gauge shows RPM value
            GaugeType.EngineLoad => engineLoad,            // Direct mapping: Load gauge shows load percentage
            GaugeType.CoolantTemperature => coolantC,      // Direct mapping: Temp gauge shows coolant temperature
            GaugeType.VehicleSpeed => speedMph,            // Direct mapping: Speed gauge shows converted MPH
            GaugeType.FuelPressure => fuelPressure,        // Direct mapping: Fuel pressure gauge shows PSI
            GaugeType.FuelTrim => fuelTrim,                // Direct mapping: Fuel trim gauge shows adjustment %
            _ => 0                                          // Default: No gauge or unknown type shows zero
        };
    }
}