using System.Globalization;
using System.Text;

namespace CarTelemetry.Core.Obd;

public enum DtcClass { Stored, Pending, Permanent }

public sealed record DtcCode(string System, string Code, string? Description);

public interface IDtcService
{
    Task<IReadOnlyList<DtcCode>> ReadAsync(DtcClass kind, CancellationToken ct);
    Task<bool> ClearAsync(CancellationToken ct); // Mode 04
}

public sealed class DtcService : IDtcService
{
    private readonly IObdAdapter _obd;
    private readonly IDtcDescriptionService _descriptionService;
    
    public DtcService(IObdAdapter obd, IDtcDescriptionService descriptionService) 
    {
        _obd = obd;
        _descriptionService = descriptionService;
    }

    public async Task<IReadOnlyList<DtcCode>> ReadAsync(DtcClass kind, CancellationToken ct)
    {
        // Mode 03 = stored, 07 = pending, 0A = permanent
        var cmd = kind switch
        {
            DtcClass.Stored    => "03",
            DtcClass.Pending   => "07",
            DtcClass.Permanent => "0A",
            _ => "03"
        };

        var raw = await _obd.SendRawAsync(cmd, ct);
        return ParseDtcs(raw, _descriptionService);
    }

    public async Task<bool> ClearAsync(CancellationToken ct)
    {
        // Mode 04 = clear DTCs and MIL
        var raw = await _obd.SendRawAsync("04", ct);
        // Many adapters just return prompt or "OK"
        return raw.Contains("44", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("OK", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(raw);
    }

    // ---------- Parsing ----------

    private static IReadOnlyList<DtcCode> ParseDtcs(string raw, IDtcDescriptionService descriptionService)
    {
        var hex = CleanToHex(raw);                // e.g., "430133000000"
        if (hex.Length < 2) return Array.Empty<DtcCode>();

        // Find response header: 43/47/4A (for 03/07/0A)
        var hdrIdx = hex.IndexOf("43", StringComparison.OrdinalIgnoreCase);
        if (hdrIdx < 0) hdrIdx = hex.IndexOf("47", StringComparison.OrdinalIgnoreCase);
        if (hdrIdx < 0) hdrIdx = hex.IndexOf("4A", StringComparison.OrdinalIgnoreCase);
        if (hdrIdx < 0) return Array.Empty<DtcCode>();

        int pos = hdrIdx + 2; // past header byte

        // Note: Some ECUs include a count byte, others don't. 
        // For our mock data, we'll assume no count byte for simplicity.
        // In real implementations, you might need to detect the format.
        
        var list = new List<DtcCode>();

        // Each DTC is 2 bytes => 4 hex chars (A,B)
        while (pos + 4 <= hex.Length)
        {
            // Read two bytes as hex
            var a = ParseHexByte(hex.AsSpan(pos, 2));
            var b = ParseHexByte(hex.AsSpan(pos + 2, 2));
            pos += 4;

            if (a == 0 && b == 0) continue;       // padding

            var (systemCode, code) = DecodeDtc(a, b); // e.g., ("P", "P0133")
            var systemName = descriptionService.GetSystemName(systemCode);
            var description = descriptionService.GetDescription(code);
            list.Add(new DtcCode(systemName, code, description));
        }

        return list;
    }

    private static byte ParseHexByte(ReadOnlySpan<char> span)
        => byte.Parse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>
    /// DTC encoding:
    /// a7-a6 system (00=P, 01=C, 10=B, 11=U)
    /// a5-a4 first digit, a3-a0 second digit, b forms last two digits
    /// </summary>
    private static (string system, string code) DecodeDtc(byte a, byte b)
    {
        string system = (a >> 6) switch { 0 => "P", 1 => "C", 2 => "B", _ => "U" };
        int d1 = (a >> 4) & 0x3;
        int d2 = a & 0xF;
        int d3 = (b >> 4) & 0xF;
        int d4 = b & 0xF;
        string code = $"{system}{d1:X}{d2:X}{d3:X}{d4:X}";
        return (system, code);
    }

    private static string CleanToHex(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            if (Uri.IsHexDigit(c)) sb.Append(c);
        return sb.ToString().ToUpperInvariant();
    }
}
