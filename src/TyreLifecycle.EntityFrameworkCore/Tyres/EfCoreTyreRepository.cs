using Microsoft.EntityFrameworkCore;
using TyreLifecycle.Tyres;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;

namespace TyreLifecycle.EntityFrameworkCore.Tyres;

public class EfCoreTyreRepository : EfCoreRepository<TyreLifecycleDbContext, Tyre, Guid>, ITyreRepository
{
    public EfCoreTyreRepository(IDbContextProvider<TyreLifecycleDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<Tyre?> FindByTyreNumberAsync(string tyreNumber, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.FirstOrDefaultAsync(x => x.TyreNumber == tyreNumber, GetCancellationToken(cancellationToken));
    }
}
