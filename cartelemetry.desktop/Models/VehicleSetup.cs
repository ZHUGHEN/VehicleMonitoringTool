namespace CarTelemetry.Desktop.Models;

/// <summary>
/// Persisted vehicle identity used by settings and future maintenance warnings.
/// </summary>
public sealed class VehicleSetup
{
    public int Year { get; set; }

    public string Make { get; set; } = "";

    public string Model { get; set; } = "";
}
