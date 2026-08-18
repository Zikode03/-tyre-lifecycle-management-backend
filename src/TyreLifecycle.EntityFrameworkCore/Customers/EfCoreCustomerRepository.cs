using Microsoft.EntityFrameworkCore;
using TyreLifecycle.Customers;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;

namespace TyreLifecycle.EntityFrameworkCore.Customers;

public class EfCoreCustomerRepository : EfCoreRepository<TyreLifecycleDbContext, Customer, Guid>, ICustomerRepository
{
    public EfCoreCustomerRepository(IDbContextProvider<TyreLifecycleDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<Customer?> FindByCustomerNumberAsync(string customerNumber, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.FirstOrDefaultAsync(x => x.CustomerNumber == customerNumber, GetCancellationToken(cancellationToken));
    }
}
