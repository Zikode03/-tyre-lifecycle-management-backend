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
        var key=VehicleRules.RegistrationKey(registrationNumber);
        return await dbSet.FirstOrDefaultAsync(x => EF.Property<string>(x,"RegistrationKey") == key, GetCancellationToken(cancellationToken));
    }
}
