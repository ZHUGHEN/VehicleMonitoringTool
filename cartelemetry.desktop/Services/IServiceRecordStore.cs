using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CarTelemetry.Desktop.Models;

namespace CarTelemetry.Desktop.Services;

public interface IServiceRecordStore
{
    Task<IReadOnlyList<ServiceRecord>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<ServiceRecord> records, CancellationToken cancellationToken = default);
}
