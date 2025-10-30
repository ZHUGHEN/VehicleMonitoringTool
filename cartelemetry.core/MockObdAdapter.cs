using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Core;

public sealed class MockObdAdapter : IObdAdapter
{
    private readonly Random _r = new();
    public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<double?> ReadRpmAsync(CancellationToken ct)
        => Task.FromResult<double?>(700 + 2500 * Math.Abs(Math.Sin(DateTime.UtcNow.Ticks / 5e7)));

    public Task<double?> ReadSpeedKmhAsync(CancellationToken ct)
        => Task.FromResult<double?>(_r.Next(0, 120));

    public Task<double?> ReadCoolantCAsync(CancellationToken ct)
        => Task.FromResult<double?>(80 + _r.NextDouble() * 5);
}
