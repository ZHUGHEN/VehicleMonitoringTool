using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Core;

public interface IObdAdapter : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct);
    Task<double?> ReadRpmAsync(CancellationToken ct);
    Task<double?> ReadSpeedKmhAsync(CancellationToken ct);
    Task<double?> ReadCoolantCAsync(CancellationToken ct);

    // New: raw command pipe (ELm/OBD ASCII, e.g., "03", "04", "010C")
    Task<string> SendRawAsync(string command, CancellationToken ct);
}
