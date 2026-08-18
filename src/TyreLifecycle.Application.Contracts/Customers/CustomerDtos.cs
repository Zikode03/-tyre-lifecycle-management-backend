using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace TyreLifecycle.Customers;

public class CustomerDto : FullAuditedEntityDto<Guid>
{
    public string CustomerNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCustomerDto
{
    [Required, StringLength(32)] public string CustomerNumber { get; set; } = string.Empty;
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, StringLength(32)] public string MobileNumber { get; set; } = string.Empty;
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
}

public class UpdateCustomerDto
{
    [Required, StringLength(32)] public string MobileNumber { get; set; } = string.Empty;
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    public bool IsActive { get; set; }
}
