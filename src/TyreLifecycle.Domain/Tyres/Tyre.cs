using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace TyreLifecycle.Tyres;

public class Tyre : FullAuditedAggregateRoot<Guid>
{
    public Guid VehicleId { get; private set; }
    public string TyreNumber { get; private set; } = string.Empty;
    public string Brand { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string Size { get; private set; } = string.Empty;
    public string Position { get; private set; } = string.Empty;
    public decimal? TreadDepthMm { get; private set; }
    public TyreHealthStatus HealthStatus { get; private set; }
    public long FitmentOdometerKm { get; private set; }
    public bool IsActive { get; private set; }

    protected Tyre() { }

    public Tyre(Guid id, Guid vehicleId, string tyreNumber, string brand, string model, string size, string position, long fitmentOdometerKm)
        : base(id)
    {
        VehicleId = vehicleId;
        TyreNumber = Check.NotNullOrWhiteSpace(tyreNumber, nameof(tyreNumber));
        Brand = Check.NotNullOrWhiteSpace(brand, nameof(brand));
        Model = Check.NotNullOrWhiteSpace(model, nameof(model));
        Size = Check.NotNullOrWhiteSpace(size, nameof(size));
        Position = Check.NotNullOrWhiteSpace(position, nameof(position));
        FitmentOdometerKm = fitmentOdometerKm;
        HealthStatus = TyreHealthStatus.Unknown;
        IsActive = true;
    }

    public void RecordTread(decimal treadDepthMm, TyreHealthStatus status)
    {
        if (treadDepthMm < 0)
            throw new BusinessException(TyreLifecycleDomainErrorCodes.InvalidTreadDepth);

        TreadDepthMm = treadDepthMm;
        HealthStatus = status;
    }

    public void MoveTo(string position) => Position = Check.NotNullOrWhiteSpace(position, nameof(position));
    public void Retire() => IsActive = false;
}
