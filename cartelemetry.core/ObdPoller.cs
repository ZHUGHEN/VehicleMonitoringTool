using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Core;

public interface IObdPoller
{
    IAsyncEnumerable<Telemetry> StreamAsync(CancellationToken ct);
}

public sealed class ObdPoller : IObdPoller
{
    private readonly IObdAdapter _obd;
    private readonly TimeSpan _period;

    public ObdPoller(IObdAdapter obd, TimeSpan? period = null)
    {
        _obd = obd;
        _period = period ?? TimeSpan.FromMilliseconds(100); // 10 Hz
    }

    public async IAsyncEnumerable<Telemetry> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await _obd.ConnectAsync(ct);
        while (!ct.IsCancellationRequested)
        {
            var t = new Telemetry(
                await _obd.ReadRpmAsync(ct),
                await _obd.ReadSpeedKmhAsync(ct),
                await _obd.ReadCoolantCAsync(ct),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            yield return t;
            await Task.Delay(_period, ct);
        }
    }
}
