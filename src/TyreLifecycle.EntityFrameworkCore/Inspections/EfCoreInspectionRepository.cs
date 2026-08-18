using Microsoft.EntityFrameworkCore;
using TyreLifecycle.Inspections;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace TyreLifecycle.EntityFrameworkCore.Inspections;

public class EfCoreInspectionRepository : EfCoreRepository<TyreLifecycleDbContext, Inspection, Guid>, IInspectionRepository
{
    public EfCoreInspectionRepository(IDbContextProvider<TyreLifecycleDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public override async Task<IQueryable<Inspection>> WithDetailsAsync()
    {
        return (await GetQueryableAsync())
            .Include(x => x.Readings);
    }

    public async Task<Inspection?> FindByInspectionNumberAsync(
        string inspectionNumber,
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        var query = includeDetails
            ? await WithDetailsAsync()
            : await GetQueryableAsync();

        return await query.FirstOrDefaultAsync(
            x => x.InspectionNumber == inspectionNumber,
            GetCancellationToken(cancellationToken));
    }
}
