using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Core.Obd;

public sealed class Elm327Adapter : IObdAdapter
{
    private readonly string _portName;
    private readonly int _baud;
    private SerialPort? _port;

    // ELM expects \r and returns '>' prompt
    private const string Prompt = ">";
    private const int DefaultTimeoutMs = 700;

    public Elm327Adapter(string portName, int baud = 38400)
    {
        _portName = portName;
        _baud = baud;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        // No side effects until ConnectAsync is actually called
        _port = new SerialPort(_portName, _baud)
        {
            NewLine = "\r",
            ReadTimeout = DefaultTimeoutMs,
            WriteTimeout = DefaultTimeoutMs
        };

        _port.Open();

        // Basic, short init; each step is best-effort and time-bounded
        await SendAsync("ATZ", ct);    // reset
        await SendAsync("ATE0", ct);   // echo off
        await SendAsync("ATL0", ct);   // linefeeds off
        await SendAsync("ATS0", ct);   // spaces off
        await SendAsync("ATH0", ct);   // headers off
        await SendAsync("ATSP0", ct);  // auto protocol
    }

    public Task<string> SendRawAsync(string command, CancellationToken ct)
        => SendAsync(command, ct);
    public async Task<double?> ReadRpmAsync(CancellationToken ct)
    {
        // Mode 01 PID 0C = Engine RPM
        // Response data bytes A,B => RPM = ((256*A)+B)/4
        var raw = await SendAsync("010C", ct);
        var bytes = ExtractDataBytes(raw);
        if (bytes is null || bytes.Length < 2) return null;

        int A = bytes[0], B = bytes[1];
        return (((256 * A) + B) / 4.0);
    }

    public async Task<double?> ReadSpeedKmhAsync(CancellationToken ct)
    {
        // Mode 01 PID 0D = Vehicle speed (km/h)
        var raw = await SendAsync("010D", ct);
        var bytes = ExtractDataBytes(raw);
        if (bytes is null || bytes.Length < 1) return null;

        return (double)bytes[0];
    }

    public async Task<double?> ReadCoolantCAsync(CancellationToken ct)
    {
        // Mode 01 PID 05 = Coolant temp => A - 40 (°C)
        var raw = await SendAsync("0105", ct);
        var bytes = ExtractDataBytes(raw);
        if (bytes is null || bytes.Length < 1) return null;

        return (double)(bytes[0] - 40);
    }

    public ValueTask DisposeAsync()
    {
        try { _port?.Dispose(); } catch { /* ignore */ }
        return ValueTask.CompletedTask;
    }

    // ---- Helpers ----
    /*private async*/
    private Task<string> SendAsync(string cmd, CancellationToken ct)
    {
        if (_port is null || !_port.IsOpen)
            throw new InvalidOperationException("Serial port is not open. Call ConnectAsync first.");

        // Clear any stale input so we read only the response to this command
        try { _port.DiscardInBuffer(); } catch { /* ignore */ }

        _port.Write(cmd + "\r");

        // Read until '>' prompt or timeout
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
                break; // partial is fine; parsers handle nulls
            }

            if (Environment.TickCount - start > DefaultTimeoutMs + 300)
                break;
        }

        //return sb.ToString();
        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// Extracts hex data bytes for Mode 01 responses.
    /// Accepts common ELM formats (with/without spaces/CRLF, with/without "41 xx" echoes).
    /// Returns null on parse failure.
    /// </summary>
    private static byte[]? ExtractDataBytes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Normalize: remove CR/LF and extra spaces
        var cleaned = raw
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("SEARCHING...", "", StringComparison.OrdinalIgnoreCase)
            .Replace("NO DATA", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (cleaned.Length == 0) return null;

        // Tokenize by whitespace
        var tokens = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // Find the first frame that starts with "41" (response to mode 01)
        int idx = Array.FindIndex(tokens, t => t.Equals("41", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0 && idx + 2 <= tokens.Length - 1)
        {
            // tokens[idx]   = "41"
            // tokens[idx+1] = PID (e.g., "0C")
            // data bytes follow from idx+2 onward
            var dataTokens = tokens[(idx + 2)..];

            // If the adapter returns a single concatenated string like "410C0FA0",
            // fall back to hex-pair slicing.
            if (dataTokens.Length == 0 && tokens[idx + 1].Length > 2)
                return SliceHexPairs(tokens[idx + 1][2..]);

            return ParseHexPairs(dataTokens);
        }

        // Fallback: try to strip all non-hex and parse pairs
        var hexOnly = new StringBuilder(cleaned.Length);
        foreach (char c in cleaned)
        {
            if (Uri.IsHexDigit(c)) hexOnly.Append(c);
        }

        // Expect something like 410C0FA0...
        var hex = hexOnly.ToString();
        int pos = hex.IndexOf("41", StringComparison.OrdinalIgnoreCase);
        if (pos >= 0 && pos + 4 <= hex.Length) // "41" + PID(2)
        {
            var rest = hex[(pos + 4)..]; // skip "41" + PID
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
