using Volo.Abp.Domain.Repositories;

namespace TyreLifecycle.Tyres;

public interface ITyreRepository : IBasicRepository<Tyre, Guid>
{
    Task<Tyre?> FindByTyreNumberAsync(string tyreNumber, CancellationToken cancellationToken = default);
}
