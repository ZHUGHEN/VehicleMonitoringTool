namespace CarTelemetry.Core;

public record Telemetry(
    double? Rpm,
    double? SpeedKmh,
    double? CoolantC,
    long TsUtcMs);
