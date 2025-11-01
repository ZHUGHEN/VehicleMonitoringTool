using System.Text.Json.Serialization;
using CarTelemetry.Desktop.ViewModels;

namespace CarTelemetry.Desktop.Configuration;

public class GaugeConfiguration
{
    public GaugeType[] GaugeSlots { get; set; } = new GaugeType[6];
    
    [JsonIgnore]
    public GaugeType GaugeSlot1 
    { 
        get => GaugeSlots[0]; 
        set => GaugeSlots[0] = value; 
    }
    
    [JsonIgnore]
    public GaugeType GaugeSlot2 
    { 
        get => GaugeSlots[1]; 
        set => GaugeSlots[1] = value; 
    }
    
    [JsonIgnore]
    public GaugeType GaugeSlot3 
    { 
        get => GaugeSlots[2]; 
        set => GaugeSlots[2] = value; 
    }
    
    [JsonIgnore]
    public GaugeType GaugeSlot4 
    { 
        get => GaugeSlots[3]; 
        set => GaugeSlots[3] = value; 
    }
    
    [JsonIgnore]
    public GaugeType GaugeSlot5 
    { 
        get => GaugeSlots[4]; 
        set => GaugeSlots[4] = value; 
    }
    
    [JsonIgnore]
    public GaugeType GaugeSlot6 
    { 
        get => GaugeSlots[5]; 
        set => GaugeSlots[5] = value; 
    }

    public static GaugeConfiguration CreateDefault()
    {
        return new GaugeConfiguration
        {
            GaugeSlots = new[]
            {
                GaugeType.EngineRpm,
                GaugeType.EngineLoad,
                GaugeType.CoolantTemperature,
                GaugeType.VehicleSpeed,
                GaugeType.None,
                GaugeType.None
            }
        };
    }

    public GaugeConfiguration Copy()
    {
        return new GaugeConfiguration
        {
            GaugeSlots = (GaugeType[])GaugeSlots.Clone()
        };
    }
}