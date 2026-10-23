using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace TyreLifecycle.Customers;

public class Customer : FullAuditedAggregateRoot<Guid>
{
    public string CustomerNumber { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string MobileNumber { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }

    protected Customer() { }

    public Customer(Guid id, string customerNumber, string firstName, string lastName, string mobileNumber, string? email = null)
        : base(id)
    {
        CustomerNumber = Check.NotNullOrWhiteSpace(customerNumber, nameof(customerNumber));
        FirstName = Check.NotNullOrWhiteSpace(firstName, nameof(firstName));
        LastName = Check.NotNullOrWhiteSpace(lastName, nameof(lastName));
        MobileNumber = CustomerContact.NormalizeMobile(mobileNumber);
        Email = CustomerContact.NormalizeEmail(email);
        IsActive = true;
    }

    public void UpdateContact(string mobileNumber, string? email)
    {
        MobileNumber = CustomerContact.NormalizeMobile(mobileNumber);
        Email = CustomerContact.NormalizeEmail(email);
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
