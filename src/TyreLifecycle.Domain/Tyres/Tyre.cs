using Volo.Abp.Domain.Entities.Auditing;

namespace TyreLifecycle.Tyres;

public enum TyreHealthStatus
{
    Unknown = 0,
    Good = 1,
    Attention = 2,
    Critical = 3
}

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
        TyreNumber = tyreNumber;
        Brand = brand;
        Model = model;
        Size = size;
        Position = position;
        FitmentOdometerKm = fitmentOdometerKm;
        HealthStatus = TyreHealthStatus.Unknown;
        IsActive = true;
    }

    public void RecordTread(decimal treadDepthMm, TyreHealthStatus status)
    {
        if (treadDepthMm < 0)
            throw new ArgumentOutOfRangeException(nameof(treadDepthMm));

        TreadDepthMm = treadDepthMm;
        HealthStatus = status;
    }

    public void MoveTo(string position) => Position = position;
    public void Retire() => IsActive = false;
}
