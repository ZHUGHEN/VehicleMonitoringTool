using System.Globalization;
using System.Text;

namespace CarTelemetry.Core.Obd;

public enum DtcClass { Stored, Pending, Permanent }

/// <summary>
/// Decoded diagnostic trouble code with a display system and optional description.
/// </summary>
public sealed record DtcCode(string System, string Code, string? Description);

public interface IDtcService
{
    Task<IReadOnlyList<DtcCode>> ReadAsync(DtcClass kind, CancellationToken ct);
    Task<bool> ClearAsync(CancellationToken ct);
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
        // OBD-II uses separate modes for stored, pending, and permanent trouble codes.
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
        var raw = await _obd.SendRawAsync("04", ct);
        return raw.Contains("44", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("OK", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(raw);
    }


    private static IReadOnlyList<DtcCode> ParseDtcs(string raw, IDtcDescriptionService descriptionService)
    {
        var hex = CleanToHex(raw);
        if (hex.Length < 2) return Array.Empty<DtcCode>();

        // Positive responses mirror the request mode: 03 -> 43, 07 -> 47, 0A -> 4A.
        var hdrIdx = hex.IndexOf("43", StringComparison.OrdinalIgnoreCase);
        if (hdrIdx < 0) hdrIdx = hex.IndexOf("47", StringComparison.OrdinalIgnoreCase);
        if (hdrIdx < 0) hdrIdx = hex.IndexOf("4A", StringComparison.OrdinalIgnoreCase);
        if (hdrIdx < 0) return Array.Empty<DtcCode>();

        int pos = hdrIdx + 2;

        
        var list = new List<DtcCode>();

        while (pos + 4 <= hex.Length)
        {
            var a = ParseHexByte(hex.AsSpan(pos, 2));
            var b = ParseHexByte(hex.AsSpan(pos + 2, 2));
            pos += 4;

            // ECUs commonly pad the remaining response bytes with zeros.
            if (a == 0 && b == 0) continue;

            var (systemCode, code) = DecodeDtc(a, b);
            var systemName = descriptionService.GetSystemName(systemCode);
            var description = descriptionService.GetDescription(code);
            list.Add(new DtcCode(systemName, code, description));
        }

        return list;
    }

    private static byte ParseHexByte(ReadOnlySpan<char> span)
        => byte.Parse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static (string system, string code) DecodeDtc(byte a, byte b)
    {
        // The top two bits identify P/C/B/U; the remaining nibbles form the four code digits.
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

