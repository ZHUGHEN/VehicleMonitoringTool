// JSON serialization import for saving/loading configuration to/from files
using System.Text.Json.Serialization;

// Import for GaugeType enum
using CarTelemetry.Desktop.ViewModels;

namespace CarTelemetry.Desktop.Configuration;

/// <summary>
/// GaugeConfiguration represents the user's custom gauge layout for the dashboard.
/// This class is responsible for:
/// 1. Storing which gauge type is displayed in each of the 6 dashboard positions
/// 2. Providing a JSON-serializable format for persistence
/// 3. Offering convenient individual slot access for data binding
/// 4. Creating default configurations for first-time users
/// 5. Supporting configuration copying for settings management
/// 
/// Persistence Strategy:
/// - The GaugeSlots array is serialized to JSON and saved to %APPDATA%\CarTelemetry\gauge-config.json
/// - Individual slot properties are marked [JsonIgnore] to avoid duplication in JSON
/// - This allows clean JSON while maintaining convenient property access for UI binding
/// 
/// UI Binding Strategy:
/// - SettingsView binds to individual slot properties (GaugeSlot1, GaugeSlot2, etc.)
/// - Each slot property provides direct access to array elements
/// - This enables dropdown controls to directly modify specific gauge positions
/// 
/// Design Pattern: Configuration Object
/// This encapsulates all gauge-related configuration in a single, serializable object
/// that can be easily saved, loaded, copied, and passed between components.
/// </summary>
public class GaugeConfiguration
{
    /// <summary>
    /// Core array storing the 6 gauge types for the dashboard positions.
    /// This is the only property that gets serialized to JSON.
    /// Array layout:
    /// [0] = Top-left gauge     [1] = Top-center gauge    [2] = Top-right gauge
    /// [3] = Bottom-left gauge  [4] = Bottom-center gauge [5] = Bottom-right gauge
    /// </summary>
    public GaugeType[] GaugeSlots { get; set; } = new GaugeType[6];
    
    // ===== INDIVIDUAL SLOT PROPERTIES FOR UI BINDING =====
    // These properties provide convenient access to individual array elements
    // [JsonIgnore] prevents them from being serialized (avoiding JSON duplication)
    // Each property maps to a specific position in the 6-gauge dashboard layout
    
    /// <summary>Top-left gauge position (Position 1) - typically RPM or primary gauge</summary>
    [JsonIgnore]
    public GaugeType GaugeSlot1 
    { 
        get => GaugeSlots[0]; 
        set => GaugeSlots[0] = value; 
    }
    
    /// <summary>Top-center gauge position (Position 2) - typically engine load or secondary gauge</summary>
    [JsonIgnore]
    public GaugeType GaugeSlot2 
    { 
        get => GaugeSlots[1]; 
        set => GaugeSlots[1] = value; 
    }
    
    /// <summary>Top-right gauge position (Position 3) - typically temperature or coolant gauge</summary>
    [JsonIgnore]
    public GaugeType GaugeSlot3 
    { 
        get => GaugeSlots[2]; 
        set => GaugeSlots[2] = value; 
    }
    
    /// <summary>Bottom-left gauge position (Position 4) - typically speed or vehicle parameter</summary>
    [JsonIgnore]
    public GaugeType GaugeSlot4 
    { 
        get => GaugeSlots[3]; 
        set => GaugeSlots[3] = value; 
    }
    
    /// <summary>Bottom-center gauge position (Position 5) - typically fuel or auxiliary gauge</summary>
    [JsonIgnore]
    public GaugeType GaugeSlot5 
    { 
        get => GaugeSlots[4]; 
        set => GaugeSlots[4] = value; 
    }
    
    /// <summary>Bottom-right gauge position (Position 6) - typically auxiliary or custom gauge</summary>
    [JsonIgnore]
    public GaugeType GaugeSlot6 
    { 
        get => GaugeSlots[5]; 
        set => GaugeSlots[5] = value; 
    }

    /// <summary>
    /// Creates a default gauge configuration for first-time users.
    /// This provides a sensible starting layout with the most commonly used automotive gauges.
    /// 
    /// Default Layout Rationale:
    /// - Position 1 (Top-left): Engine RPM - Most critical for manual transmissions and performance driving
    /// - Position 2 (Top-center): Engine Load - Important for understanding engine stress and efficiency
    /// - Position 3 (Top-right): Coolant Temperature - Critical for engine health monitoring
    /// - Position 4 (Bottom-left): Vehicle Speed - Essential driving information
    /// - Position 5 & 6: None - Left empty for user customization based on their specific needs
    /// 
    /// This configuration balances essential engine monitoring with driving information,
    /// while leaving room for users to add fuel, diagnostic, or specialized gauges.
    /// </summary>
    /// <returns>GaugeConfiguration with sensible defaults for automotive dashboard</returns>
    public static GaugeConfiguration CreateDefault()
    {
        return new GaugeConfiguration
        {
            GaugeSlots = new[]
            {
                GaugeType.EngineRpm,          // Top-left: Primary engine parameter
                GaugeType.EngineLoad,         // Top-center: Secondary engine parameter  
                GaugeType.CoolantTemperature, // Top-right: Critical safety parameter
                GaugeType.VehicleSpeed,       // Bottom-left: Primary driving parameter
                GaugeType.None,               // Bottom-center: User customizable
                GaugeType.None                // Bottom-right: User customizable
            }
        };
    }

    /// <summary>
    /// Creates a deep copy of the current gauge configuration.
    /// This is used by the settings system to create a working copy that can be modified
    /// without affecting the live configuration until the user saves their changes.
    /// 
    /// Why Deep Copy is Important:
    /// - Settings UI needs to modify configuration without immediately affecting the main dashboard
    /// - Users can cancel changes and revert to the original configuration
    /// - Prevents partial/incomplete configurations from being applied during editing
    /// - Enables preview functionality where changes can be tested before committing
    /// 
    /// Implementation Note:
    /// Uses Array.Clone() to create a new array with the same values, ensuring the
    /// copy is completely independent of the original.
    /// </summary>
    /// <returns>Independent copy of the current configuration</returns>
    public GaugeConfiguration Copy()
    {
        return new GaugeConfiguration
        {
            GaugeSlots = (GaugeType[])GaugeSlots.Clone() // Deep copy the array
        };
    }
}