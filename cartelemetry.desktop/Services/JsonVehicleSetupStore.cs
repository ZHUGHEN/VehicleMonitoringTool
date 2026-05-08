using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Desktop.Models;

namespace CarTelemetry.Desktop.Services;

/// <summary>
/// Stores the selected vehicle setup in user app data.
/// </summary>
public sealed class JsonVehicleSetupStore : IVehicleSetupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _setupPath;

    public JsonVehicleSetupStore()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CarTelemetry");

        _setupPath = Path.Combine(appDataPath, "vehicle-setup.json");
    }

    public async Task<VehicleSetup?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_setupPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_setupPath, cancellationToken);
        return JsonSerializer.Deserialize<VehicleSetup>(json, JsonOptions);
    }

    public async Task SaveAsync(VehicleSetup setup, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_setupPath)!);

        var json = JsonSerializer.Serialize(setup, JsonOptions);
        await File.WriteAllTextAsync(_setupPath, json, cancellationToken);
    }
}
