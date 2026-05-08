using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Desktop.Models;

namespace CarTelemetry.Desktop.Services;

/// <summary>
/// Persists service records and latest mileage by maintenance type as JSON in user app data.
/// </summary>
public sealed class JsonServiceRecordStore : IServiceRecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _recordsPath;
    private readonly string _latestMileagePath;

    public JsonServiceRecordStore()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CarTelemetry");

        _recordsPath = Path.Combine(appDataPath, "service-records.json");
        _latestMileagePath = Path.Combine(appDataPath, "service-mileage.json");
    }

    public async Task<IReadOnlyList<ServiceRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_recordsPath))
        {
            return Array.Empty<ServiceRecord>();
        }

        var json = await File.ReadAllTextAsync(_recordsPath, cancellationToken);
        var records = JsonSerializer.Deserialize<List<ServiceRecord>>(json, JsonOptions) ?? new List<ServiceRecord>();

        return Sort(records).ToArray();
    }

    public async Task SaveAsync(IReadOnlyList<ServiceRecord> records, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_recordsPath)!);

        var sortedRecords = Sort(records).ToArray();
        var recordsJson = JsonSerializer.Serialize(sortedRecords, JsonOptions);
        await File.WriteAllTextAsync(_recordsPath, recordsJson, cancellationToken);

        var latestMileageByType = sortedRecords
            .GroupBy(record => record.Type)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(record => record.Year)
                    .ThenByDescending(record => record.Month)
                    .ThenByDescending(record => record.Mileage)
                    .First()
                    .Mileage);

        var mileageJson = JsonSerializer.Serialize(latestMileageByType, JsonOptions);
        await File.WriteAllTextAsync(_latestMileagePath, mileageJson, cancellationToken);
    }

    private static IEnumerable<ServiceRecord> Sort(IEnumerable<ServiceRecord> records)
    {
        return records
            .OrderBy(record => record.Year)
            .ThenBy(record => record.Month)
            .ThenBy(record => record.Mileage)
            .ThenBy(record => record.CreatedAt);
    }
}
