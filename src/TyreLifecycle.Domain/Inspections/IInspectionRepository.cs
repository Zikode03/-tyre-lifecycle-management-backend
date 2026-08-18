using Volo.Abp.Domain.Repositories;

namespace TyreLifecycle.Inspections;

public interface IInspectionRepository : IRepository<Inspection, Guid>
{
    Task<Inspection?> FindByInspectionNumberAsync(
        string inspectionNumber,
        bool includeDetails = true,
        CancellationToken cancellationToken = default);
}
