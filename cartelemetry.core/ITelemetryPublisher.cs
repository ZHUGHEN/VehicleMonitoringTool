namespace CarTelemetry.Core;

public interface ITelemetryPublisher
{
    Task PublishAsync(Telemetry t, CancellationToken ct = default);
}
