namespace CarTelemetry.Core;

/// <summary>
/// Immutable snapshot of the vehicle telemetry values collected at a single point in time.
/// </summary>
public record Telemetry(
    double? Rpm,
    double? SpeedKmh,
    double? CoolantC,
    long TsUtcMs);

