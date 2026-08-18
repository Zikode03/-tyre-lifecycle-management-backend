using Microsoft.AspNetCore.Authorization;
using TyreLifecycle.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace TyreLifecycle.Customers;

[Authorize(TyreLifecyclePermissions.Customers.Default)]
public class CustomerAppService : ApplicationService, ICustomerAppService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly CustomerManager _customerManager;

    public CustomerAppService(ICustomerRepository customerRepository, CustomerManager customerManager)
    {
        _customerRepository = customerRepository;
        _customerManager = customerManager;
    }

    public async Task<CustomerDto> GetAsync(Guid id)
        => ObjectMapper.Map<Customer, CustomerDto>(await _customerRepository.GetAsync(id));

    public async Task<PagedResultDto<CustomerDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _customerRepository.GetCountAsync();
        var items = await _customerRepository.GetPagedListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting ?? nameof(Customer.CreationTime),
            includeDetails: true);

        return new PagedResultDto<CustomerDto>(
            totalCount,
            ObjectMapper.Map<List<Customer>, List<CustomerDto>>(items));
    }

    [Authorize(TyreLifecyclePermissions.Customers.Create)]
    public async Task<CustomerDto> CreateAsync(CreateCustomerDto input)
    {
        var customer = await _customerManager.CreateAsync(
            input.CustomerNumber,
            input.FirstName,
            input.LastName,
            input.MobileNumber,
            input.Email);

        await _customerRepository.InsertAsync(customer, autoSave: true);
        return ObjectMapper.Map<Customer, CustomerDto>(customer);
    }

    [Authorize(TyreLifecyclePermissions.Customers.Update)]
    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerDto input)
    {
        var customer = await _customerRepository.GetAsync(id);
        customer.UpdateContact(input.MobileNumber, input.Email);
        customer.SetActive(input.IsActive);

        await _customerRepository.UpdateAsync(customer, autoSave: true);
        return ObjectMapper.Map<Customer, CustomerDto>(customer);
    }

    [Authorize(TyreLifecyclePermissions.Customers.Delete)]
    public Task DeleteAsync(Guid id)
        => _customerRepository.DeleteAsync(id, autoSave: true);
}
