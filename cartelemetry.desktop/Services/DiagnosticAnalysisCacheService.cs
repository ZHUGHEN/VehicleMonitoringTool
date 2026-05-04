using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Core.Obd;

namespace CarTelemetry.Desktop.Services;

public interface IDiagnosticAnalysisCacheService
{
    /// <summary>
    /// Builds a stable cache key for a diagnostic analysis request.
    /// </summary>
    string CreateKey(string aiModel, string vehicleModel, IReadOnlyCollection<DtcCode> codes);

    /// <summary>
    /// Returns a cached analysis when one exists for the supplied key.
    /// </summary>
    Task<string?> GetAsync(string key, CancellationToken ct);

    /// <summary>
    /// Stores the completed analysis for future requests with the same key.
    /// </summary>
    Task SetAsync(string key, string analysis, CancellationToken ct);

    /// <summary>
    /// Removes all persisted diagnostic analysis entries.
    /// </summary>
    Task ClearAsync(CancellationToken ct);
}

public sealed class JsonDiagnosticAnalysisCacheService : IDiagnosticAnalysisCacheService
{
    // Increment when the on-disk cache shape or key semantics change.
    private const int CacheVersion = 1;

    // Keep prompt changes from reusing analyses generated with older instructions.
    private const string PromptVersion = "diagnostic-prompt-v1";

    // Serializes file access so read/modify/write operations cannot overlap.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly string _cacheFilePath;

    public JsonDiagnosticAnalysisCacheService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CarTelemetry");

        _cacheFilePath = Path.Combine(appDataPath, "diagnostic-analysis-cache.json");
    }

    public string CreateKey(string aiModel, string vehicleModel, IReadOnlyCollection<DtcCode> codes)
    {
        // Normalize user and scan input so equivalent requests hit the same cache entry.
        var normalizedCodes = codes
            .Select(code => code.Code.Trim().ToUpperInvariant())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase);

        var keyMaterial = string.Join('\n', new[]
        {
            $"cache:{CacheVersion}",
            $"prompt:{PromptVersion}",
            $"aiModel:{aiModel.Trim()}",
            $"vehicle:{vehicleModel.Trim().ToUpperInvariant()}",
            $"codes:{string.Join(",", normalizedCodes)}"
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
        return Convert.ToHexString(hash);
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cache = await ReadCacheAsync(ct);
            return cache.Entries.TryGetValue(key, out var entry) ? entry.Analysis : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync(string key, string analysis, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cache = await ReadCacheAsync(ct);
            cache.Entries[key] = new DiagnosticAnalysisCacheEntry
            {
                Analysis = analysis,
                CreatedUtc = DateTimeOffset.UtcNow
            };

            await WriteCacheAsync(cache, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DiagnosticAnalysisCacheFile> ReadCacheAsync(CancellationToken ct)
    {
        if (!File.Exists(_cacheFilePath))
        {
            return new DiagnosticAnalysisCacheFile();
        }

        await using var stream = File.OpenRead(_cacheFilePath);
        var cache = await JsonSerializer.DeserializeAsync<DiagnosticAnalysisCacheFile>(stream, cancellationToken: ct);

        // Treat missing or incompatible cache files as empty rather than failing the analysis flow.
        return cache?.Version == CacheVersion ? cache : new DiagnosticAnalysisCacheFile();
    }

    private async Task WriteCacheAsync(DiagnosticAnalysisCacheFile cache, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_cacheFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_cacheFilePath);
        await JsonSerializer.SerializeAsync(stream, cache, _jsonOptions, ct);
    }

    private sealed class DiagnosticAnalysisCacheFile
    {
        public int Version { get; set; } = CacheVersion;
        public Dictionary<string, DiagnosticAnalysisCacheEntry> Entries { get; set; } = new();
    }

    private sealed class DiagnosticAnalysisCacheEntry
    {
        public string Analysis { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; }
    }
}
