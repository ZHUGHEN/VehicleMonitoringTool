using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Desktop.Services;

/// <summary>
/// Loads vehicle make/model options from NHTSA vPIC and caches them as JSON for offline use.
/// </summary>
public sealed class NhtsaVehicleCatalogService : IVehicleCatalogService
{
    private const int CacheVersion = 3;
    private const string PassengerCarVehicleType = "Passenger Car";
    private static readonly IReadOnlyList<string> AllowedMakes = new[]
    {
        "Toyota",
        "Lexus",
        "Daihatsu",
        "Hinoc",
        "Volkswagen",
        "Audi",
        "Skoda",
        "SEAT",
        "Cupra",
        "Porsche",
        "Lamborghini",
        "Bentley",
        "Chevrolet",
        "Buick",
        "GMC",
        "Cadillac",
        "Oldsmobile",
        "Pontiac",
        "Saab",
        "Hummer",
        "Saturn",
        "Fiat",
        "Chrysler",
        "Jeep",
        "Dodge",
        "Ram",
        "Alfa Romeo",
        "Maserati",
        "Peugeot",
        "Citroen",
        "Opel",
        "Vauxhall",
        "Hyundai",
        "Kia",
        "Genesis",
        "Ford",
        "Lincoln",
        "Jaguar",
        "Land Rover",
        "Volvo",
        "Mazda",
        "Nissan",
        "Infiniti",
        "Mitsubishi",
        "Renault",
        "Dacia",
        "Honda",
        "Acura"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _cachePath;
    private VehicleCatalogCache _cache = new();
    private bool _isCacheLoaded;

    public NhtsaVehicleCatalogService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CarTelemetry");

        _cachePath = Path.Combine(appDataPath, "vehicle-catalog-cache.json");
    }

    public async Task<IReadOnlyList<string>> GetMakesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCacheLoadedAsync(cancellationToken);

        if (_cache.Makes.Count == 0)
        {
            _cache.Makes = AllowedMakes.OrderBy(make => make).ToList();
            await SaveCacheAsync(cancellationToken);
        }

        return _cache.Makes;
    }

    public async Task<IReadOnlyList<string>> GetModelsAsync(int year, string make, CancellationToken cancellationToken = default)
    {
        await EnsureCacheLoadedAsync(cancellationToken);

        var cacheKey = $"{year}|{make}|{PassengerCarVehicleType}".ToUpperInvariant();

        if (_cache.ModelsByYearMake.TryGetValue(cacheKey, out var cachedModels))
        {
            return cachedModels;
        }

        var escapedMake = Uri.EscapeDataString(make);
        var vehicleType = Uri.EscapeDataString(PassengerCarVehicleType);
        var uri = $"https://vpic.nhtsa.dot.gov/api/vehicles/GetModelsForMakeYear/make/{escapedMake}/modelyear/{year}/vehicletype/{vehicleType}?format=json";
        var response = await _httpClient.GetFromJsonAsync<NhtsaModelsResponse>(uri, cancellationToken);

        var models = response?.Results
            .Select(result => result.ModelName?.Trim())
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => model)
            .ToList() ?? new List<string>();

        _cache.ModelsByYearMake[cacheKey] = models;
        await SaveCacheAsync(cancellationToken);
        return models;
    }

    private async Task EnsureCacheLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isCacheLoaded)
        {
            return;
        }

        if (File.Exists(_cachePath))
        {
            var json = await File.ReadAllTextAsync(_cachePath, cancellationToken);
            _cache = JsonSerializer.Deserialize<VehicleCatalogCache>(json, JsonOptions) ?? new VehicleCatalogCache();

            if (_cache.Version != CacheVersion)
            {
                _cache = new VehicleCatalogCache();
            }
        }

        _isCacheLoaded = true;
    }

    private async Task SaveCacheAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);

        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        await File.WriteAllTextAsync(_cachePath, json, cancellationToken);
    }

    private sealed class VehicleCatalogCache
    {
        public int Version { get; set; } = CacheVersion;

        public List<string> Makes { get; set; } = new();

        public Dictionary<string, List<string>> ModelsByYearMake { get; set; } = new();
    }

    private sealed class NhtsaModelsResponse
    {
        public List<NhtsaModelResult> Results { get; set; } = new();
    }

    private sealed class NhtsaModelResult
    {
        [JsonPropertyName("Model_Name")]
        public string? ModelName { get; set; }
    }
}
