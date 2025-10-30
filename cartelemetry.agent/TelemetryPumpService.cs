using CarTelemetry.Core;
using Microsoft.Extensions.Hosting;

namespace CarTelemetry.Agent;

public sealed class TelemetryPumpService : BackgroundService
{
    private readonly IObdPoller _poller;
    private readonly ITelemetryPublisher _publisher;

    public TelemetryPumpService(IObdPoller poller, ITelemetryPublisher publisher)
    {
        _poller = poller;
        _publisher = publisher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var t in _poller.StreamAsync(stoppingToken))
        {
            await _publisher.PublishAsync(t, stoppingToken);
            await Task.Delay(100, stoppingToken); // ~10 Hz
        }
    }
}
