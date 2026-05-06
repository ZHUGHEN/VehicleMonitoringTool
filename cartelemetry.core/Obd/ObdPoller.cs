using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Core.Obd;

/// <summary>
/// Streams normalized telemetry samples from an OBD adapter.
/// </summary>
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
        _period = period ?? TimeSpan.FromMilliseconds(250);
    }

    public async IAsyncEnumerable<Telemetry> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Connect lazily so the adapter is not opened until a consumer starts reading telemetry.
        await _obd.ConnectAsync(ct);

        double? rpm = null;
        double? speedKmh = null;
        double? coolantC = null;
        var requestIndex = 0;

        while (!ct.IsCancellationRequested)
        {
            // RPM and speed change quickly; coolant is sampled less often because it moves slowly.
            switch (requestIndex % 8)
            {
                case 1:
                case 3:
                case 5:
                    speedKmh = await _obd.ReadSpeedKmhAsync(ct);
                    break;
                case 7:
                    coolantC = await _obd.ReadCoolantCAsync(ct);
                    break;
                default:
                    rpm = await _obd.ReadRpmAsync(ct);
                    break;
            }

            requestIndex++;

            yield return new Telemetry(
                rpm,
                speedKmh,
                coolantC,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );
            
            // Delay between individual PID requests; bus response time adds to the real cadence.
            await Task.Delay(_period, ct);
        }
        
    }
}

