using System;

namespace CarTelemetry.Desktop.Models;

public class LapTime
{
    public int LapNumber { get; set; }
    public TimeSpan Time { get; set; }
    public bool IsBest { get; set; }
    public DateTime Timestamp { get; set; }
}