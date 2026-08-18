using Volo.Abp.Domain.Repositories;

namespace TyreLifecycle.Customers;

public interface ICustomerRepository : IBasicRepository<Customer, Guid>
{
    Task<Customer?> FindByCustomerNumberAsync(string customerNumber, CancellationToken cancellationToken = default);
}
