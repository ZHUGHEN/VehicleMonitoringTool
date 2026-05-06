using System;
using System.Security.Cryptography;
using System.Text;

namespace CarTelemetry.Desktop.Utils;

/// <summary>
/// Helper methods for generating ingest keys used by telemetry relay clients.
/// </summary>
public static class KeyGenerator
{
    public static string GenerateIngestKey(int length = 32)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new char[length];
        
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        
        for (int i = 0; i < length; i++)
        {
            random[i] = chars[bytes[i] % chars.Length];
        }
        
        return new string(random);
    }
    
    public static string GenerateVehicleKey(string vehicleId, string secret = "Z33-Racing-2025")
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(vehicleId));
        return Convert.ToBase64String(hash)[..24];
    }
    
    public static string GenerateRotatingKey(string baseSecret = "Z33-Daily-Key")
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(baseSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(today));
        return Convert.ToBase64String(hash)[..32];
    }
}

