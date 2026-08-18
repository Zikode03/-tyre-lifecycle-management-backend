using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace TyreLifecycle.Customers;

public class CustomerManager : DomainService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerManager(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<Customer> CreateAsync(string customerNumber, string firstName, string lastName, string mobileNumber, string? email = null)
    {
        Check.NotNullOrWhiteSpace(customerNumber, nameof(customerNumber));
        Check.NotNullOrWhiteSpace(firstName, nameof(firstName));
        Check.NotNullOrWhiteSpace(lastName, nameof(lastName));
        Check.NotNullOrWhiteSpace(mobileNumber, nameof(mobileNumber));

        if (await _customerRepository.FindByCustomerNumberAsync(customerNumber) is not null)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.CustomerNumberAlreadyExists)
                .WithData("CustomerNumber", customerNumber);
        }

        return new Customer(GuidGenerator.Create(), customerNumber.Trim(), firstName.Trim(), lastName.Trim(), mobileNumber.Trim(), email?.Trim());
    }
}
