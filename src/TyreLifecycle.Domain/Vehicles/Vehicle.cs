using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace TyreLifecycle.Vehicles;

public class Vehicle : FullAuditedAggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }
    public string RegistrationNumber { get; private set; } = string.Empty;
    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public long OdometerKm { get; private set; }
    public string? Vin { get; private set; }
    public bool IsActive { get; private set; }

    protected Vehicle() { }

    public Vehicle(Guid id, Guid customerId, string registrationNumber, string make, string model, int year, long odometerKm, string? vin = null)
        : base(id)
    {
        CustomerId = customerId;
        RegistrationNumber = Check.NotNullOrWhiteSpace(registrationNumber, nameof(registrationNumber));
        Make = Check.NotNullOrWhiteSpace(make, nameof(make));
        Model = Check.NotNullOrWhiteSpace(model, nameof(model));
        Year = year;
        OdometerKm = odometerKm;
        Vin = vin;
        IsActive = true;
    }

    public void UpdateOdometer(long odometerKm)
    {
        if (odometerKm < OdometerKm)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.VehicleOdometerCannotMoveBackwards)
                .WithData("CurrentOdometerKm", OdometerKm)
                .WithData("RequestedOdometerKm", odometerKm);
        }

        OdometerKm = odometerKm;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
