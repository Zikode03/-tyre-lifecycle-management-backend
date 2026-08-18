using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Customers;

public interface ICustomerAppService : IApplicationService
{
    Task<CustomerDto> GetAsync(Guid id);
    Task<PagedResultDto<CustomerDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<CustomerDto> CreateAsync(CreateCustomerDto input);
    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerDto input);
    Task DeleteAsync(Guid id);
}
