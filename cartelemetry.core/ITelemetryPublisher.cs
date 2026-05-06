namespace CarTelemetry.Core;

/// <summary>
/// Publishes telemetry snapshots to a downstream transport or storage target.
/// </summary>
public interface ITelemetryPublisher
{
    Task PublishAsync(Telemetry t, CancellationToken ct = default);
}

