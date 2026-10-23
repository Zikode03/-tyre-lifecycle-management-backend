using TyreLifecycle.Customers;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace TyreLifecycle.Vehicles;

public class VehicleManager : DomainService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ICustomerRepository _customerRepository;

    public VehicleManager(IVehicleRepository vehicleRepository, ICustomerRepository customerRepository)
    {
        _vehicleRepository = vehicleRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Vehicle> CreateAsync(Guid customerId, string registrationNumber, string make, string model, int year, long odometerKm, string? vin = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));

        var customer=await _customerRepository.FindAsync(customerId);
        if(customer is null || !customer.IsActive) throw new UserFriendlyException("Choose an active, registered customer.");

        Check.NotNullOrWhiteSpace(registrationNumber, nameof(registrationNumber));
        Check.NotNullOrWhiteSpace(make, nameof(make));
        Check.NotNullOrWhiteSpace(model, nameof(model));

        var normalizedRegistration = VehicleRules.Registration(registrationNumber);
        if (await _vehicleRepository.FindByRegistrationAsync(normalizedRegistration) is not null)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.VehicleRegistrationAlreadyExists)
                .WithData("RegistrationNumber", normalizedRegistration);
        }

        return new Vehicle(GuidGenerator.Create(), customerId, normalizedRegistration, make.Trim(), model.Trim(), year, odometerKm, vin?.Trim());
    }
}
