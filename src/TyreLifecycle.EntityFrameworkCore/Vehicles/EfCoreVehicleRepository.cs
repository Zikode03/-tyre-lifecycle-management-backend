using Microsoft.EntityFrameworkCore;
using TyreLifecycle.Vehicles;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;

namespace TyreLifecycle.EntityFrameworkCore.Vehicles;

public class EfCoreVehicleRepository : EfCoreRepository<TyreLifecycleDbContext, Vehicle, Guid>, IVehicleRepository
{
    public EfCoreVehicleRepository(IDbContextProvider<TyreLifecycleDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<Vehicle?> FindByRegistrationAsync(string registrationNumber, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.FirstOrDefaultAsync(x => x.RegistrationNumber == registrationNumber, GetCancellationToken(cancellationToken));
    }
}
