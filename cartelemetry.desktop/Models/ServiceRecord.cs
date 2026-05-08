using System;

namespace CarTelemetry.Desktop.Models;

/// <summary>
/// Maintenance entry persisted in service history.
/// </summary>
public sealed class ServiceRecord
{
    public string Type { get; set; } = "";

    public int Mileage { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string DisplayDate => $"{Month:00}/{Year}";

    public string DisplayMileage => $"{Mileage:N0} mi";
}
