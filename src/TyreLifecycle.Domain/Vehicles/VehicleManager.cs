using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace TyreLifecycle.Vehicles;

public class VehicleManager : DomainService
{
    private readonly IVehicleRepository _vehicleRepository;

    public VehicleManager(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Vehicle> CreateAsync(Guid customerId, string registrationNumber, string make, string model, int year, long odometerKm, string? vin = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));

        Check.NotNullOrWhiteSpace(registrationNumber, nameof(registrationNumber));
        Check.NotNullOrWhiteSpace(make, nameof(make));
        Check.NotNullOrWhiteSpace(model, nameof(model));

        var normalizedRegistration = registrationNumber.Trim().ToUpperInvariant();
        if (await _vehicleRepository.FindByRegistrationAsync(normalizedRegistration) is not null)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.VehicleRegistrationAlreadyExists)
                .WithData("RegistrationNumber", normalizedRegistration);
        }

        return new Vehicle(GuidGenerator.Create(), customerId, normalizedRegistration, make.Trim(), model.Trim(), year, odometerKm, vin?.Trim());
    }
}
