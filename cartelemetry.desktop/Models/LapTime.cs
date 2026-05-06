using System;

namespace CarTelemetry.Desktop.Models;

/// <summary>
/// Completed lap entry displayed in the dashboard lap history.
/// </summary>
public class LapTime
{
    public int LapNumber { get; set; }
    
    public TimeSpan Time { get; set; }
    
    public bool IsBest { get; set; }
    
    public DateTime Timestamp { get; set; }
}

