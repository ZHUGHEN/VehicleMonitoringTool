using System.Net.Http;
using System.Net.Http.Json;

namespace CarTelemetry.Core;

/// <summary>
/// Sends telemetry envelopes to the relay service over HTTP.
/// </summary>
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
        // Version the wire format so relay contracts can evolve independently of clients.
        var env = new
        {
            type = "telemetry",
            vehicleId = _vehicleId,
            sessionId = _sessionId,
            v = 1,
            ts = t.TsUtcMs,
            payload = new { rpm = t.Rpm, speedKmh = t.SpeedKmh, coolantC = t.CoolantC }
        };

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                // Create a new request per attempt because HttpRequestMessage cannot be resent.
                var req = new HttpRequestMessage(HttpMethod.Post, $"/ingest/{_vehicleId}/{_sessionId}")
                {
                    Content = JsonContent.Create(env)
                };
                // Relay validates this key before broadcasting telemetry to SignalR clients.
                req.Headers.Add("X-Ingest-Key", _ingestKey);

                var res = await _http.SendAsync(req, ct);
                res.EnsureSuccessStatusCode();
                break;
            }
            catch when (attempt < 3)
            {
                // Short backoff keeps transient relay or network hiccups from dropping samples immediately.
                await Task.Delay(250 * attempt, ct);
            }
        }
    }

    public void Dispose() => _http.Dispose();
}

