using System.Net.Http;
using System.Net.Http.Json;

namespace CarTelemetry.Core;

public sealed class RelayPublisher : ITelemetryPublisher, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _vehicleId;
    private readonly string _sessionId;
    private readonly string _ingestKey;

    public RelayPublisher(Uri baseAddress, string vehicleId, string sessionId, string ingestKey, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = baseAddress;

        _vehicleId = vehicleId;
        _sessionId = sessionId;
        _ingestKey = ingestKey;
    }

    public async Task PublishAsync(Telemetry t, CancellationToken ct = default)
    {
        var env = new
        {
            type = "telemetry",
            vehicleId = _vehicleId,
            sessionId = _sessionId,
            v = 1,
            ts = t.TsUtcMs,
            payload = new { rpm = t.Rpm, speedKmh = t.SpeedKmh, coolantC = t.CoolantC }
        };

        // simple retry with backoff
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"/ingest/{_vehicleId}/{_sessionId}")
                {
                    Content = JsonContent.Create(env)
                };
                req.Headers.Add("X-Ingest-Key", _ingestKey);
                var res = await _http.SendAsync(req, ct);
                res.EnsureSuccessStatusCode();
                break; // success
            }
            catch when (attempt < 3)
            {
                await Task.Delay(250 * attempt, ct);
            }
        }
    }

    public void Dispose() => _http.Dispose();
}
