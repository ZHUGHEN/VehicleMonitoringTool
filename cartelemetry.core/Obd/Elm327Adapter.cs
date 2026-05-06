using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Core.Obd;

/// <summary>
/// Serial-port implementation for ELM327-compatible OBD-II adapters.
/// </summary>
public sealed class Elm327Adapter : IObdAdapter
{
    private readonly string _portName;
    private readonly int _baud;
    private SerialPort? _port;

    private const string Prompt = ">";
    private const int DefaultTimeoutMs = 1500;

    public Elm327Adapter(string portName, int baud = 38400)
    {
        _portName = portName;
        _baud = baud;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _port = new SerialPort(_portName, _baud)
        {
            NewLine = "\r",
            ReadTimeout = DefaultTimeoutMs,
            WriteTimeout = DefaultTimeoutMs
        };

        _port.Open();

        // Configure a quiet ISO 9141-2 session that returns compact, parser-friendly responses.
        await SendAsync("ATZ", ct);
        await SendAsync("ATE0", ct);
        await SendAsync("ATL0", ct);
        await SendAsync("ATS1", ct);
        await SendAsync("ATH0", ct);
        await SendAsync("ATAT1", ct);
        await SendAsync("ATST96", ct);
        // The Z33 uses ISO 9141-2; change this if the adapter is used with a different vehicle.
        await SendAsync("ATSP3", ct);
    }

    public Task<string> SendRawAsync(string command, CancellationToken ct)
        => SendAsync(command, ct);
    public async Task<double?> ReadRpmAsync(CancellationToken ct)
    {
        var raw = await SendAsync("010C", ct);
        var bytes = ExtractDataBytes(raw);
        if (bytes is null || bytes.Length < 2) return null;

        int A = bytes[0], B = bytes[1];
        return (((256 * A) + B) / 4.0);
    }

    public async Task<double?> ReadSpeedKmhAsync(CancellationToken ct)
    {
        var raw = await SendAsync("010D", ct);
        var bytes = ExtractDataBytes(raw);
        if (bytes is null || bytes.Length < 1) return null;

        return (double)bytes[0];
    }

    public async Task<double?> ReadCoolantCAsync(CancellationToken ct)
    {
        var raw = await SendAsync("0105", ct);
        var bytes = ExtractDataBytes(raw);
        if (bytes is null || bytes.Length < 1) return null;

        return (double)(bytes[0] - 40);
    }

    public ValueTask DisposeAsync()
    {
        try { _port?.Dispose(); } catch { }
        return ValueTask.CompletedTask;
    }

    private Task<string> SendAsync(string cmd, CancellationToken ct)
    {
        if (_port is null || !_port.IsOpen)
            throw new InvalidOperationException("Serial port is not open. Call ConnectAsync first.");

        try { _port.DiscardInBuffer(); } catch { }

        // ELM commands are carriage-return terminated and complete when the adapter prompt returns.
        _port.Write(cmd + "\r");

        var sb = new StringBuilder(64);
        var start = Environment.TickCount;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                int ch = _port.ReadChar();
                if (ch == -1) break;
                char c = (char)ch;
                if (c == '>') break;
                sb.Append(c);
            }
            catch (TimeoutException)
            {
                // Partial responses are still useful; typed readers return null if parsing fails.
                break;
            }

            if (Environment.TickCount - start > DefaultTimeoutMs + 300)
                break;
        }

        return Task.FromResult(sb.ToString());
    }

    private static byte[]? ExtractDataBytes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var cleaned = raw
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("SEARCHING...", "", StringComparison.OrdinalIgnoreCase)
            .Replace("NO DATA", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (cleaned.Length == 0) return null;

        var tokens = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // Prefer tokenized ELM responses, then fall back to compact hexadecimal output.
        int idx = Array.FindIndex(tokens, t => t.Equals("41", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && idx + 2 <= tokens.Length - 1)
        {
            var dataTokens = tokens[(idx + 2)..];

            if (dataTokens.Length == 0 && tokens[idx + 1].Length > 2)
                return SliceHexPairs(tokens[idx + 1][2..]);

            return ParseHexPairs(dataTokens);
        }

        var hexOnly = new StringBuilder(cleaned.Length);
        foreach (char c in cleaned)
        {
            if (Uri.IsHexDigit(c)) hexOnly.Append(c);
        }

        var hex = hexOnly.ToString();
        int pos = hex.IndexOf("41", StringComparison.OrdinalIgnoreCase);
        if (pos >= 0 && pos + 4 <= hex.Length)
        {
            var rest = hex[(pos + 4)..];
            return SliceHexPairs(rest);
        }

        return null;
    }

    private static byte[]? ParseHexPairs(string[] tokens)
    {
        var list = new System.Collections.Generic.List<byte>(tokens.Length);
        foreach (var t in tokens)
        {
            if (t.Length != 2) return null;
            if (!byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out var b))
                return null;
            list.Add(b);
        }
        return list.ToArray();
    }

    private static byte[]? SliceHexPairs(string hex)
    {
        if (hex.Length < 2) return null;
        int len = hex.Length / 2;
        var buf = new byte[len];
        for (int i = 0; i < len; i++)
        {
            var pair = hex.AsSpan(i * 2, 2);
            if (!byte.TryParse(pair, System.Globalization.NumberStyles.HexNumber, null, out var b))
                return null;
            buf[i] = b;
        }
        return buf;
    }
}

