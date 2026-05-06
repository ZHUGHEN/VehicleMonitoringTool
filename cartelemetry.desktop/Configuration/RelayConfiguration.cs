namespace CarTelemetry.Desktop.Configuration;

/// <summary>
/// Relay endpoint and identity settings used when publishing telemetry.
/// </summary>
public class RelayConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public string VehicleId { get; set; } = "Z33-01";
    public string SessionId { get; set; } = "dev-local";
    public string IngestKey { get; set; } = "super-secret-123";
}

