using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CarTelemetry.Desktop.Services;

public interface IVehicleCatalogService
{
    Task<IReadOnlyList<string>> GetMakesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetModelsAsync(int year, string make, CancellationToken cancellationToken = default);
}
