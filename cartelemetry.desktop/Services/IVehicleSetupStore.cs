using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Desktop.Models;

namespace CarTelemetry.Desktop.Services;

public interface IVehicleSetupStore
{
    Task<VehicleSetup?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(VehicleSetup setup, CancellationToken cancellationToken = default);
}
