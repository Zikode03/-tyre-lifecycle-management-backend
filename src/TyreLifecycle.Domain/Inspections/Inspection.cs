using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace TyreLifecycle.Inspections;

public class Inspection : FullAuditedAggregateRoot<Guid>
{
    public string InspectionNumber { get; private set; } = string.Empty;
    public Guid VehicleId { get; private set; }
    public Guid? TechnicianUserId { get; private set; }
    public long? OdometerKm { get; private set; }
    public InspectionStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<InspectionTyreReading> _readings = [];
    public IReadOnlyCollection<InspectionTyreReading> Readings => _readings;

    protected Inspection() { }

    public Inspection(Guid id, string inspectionNumber, Guid vehicleId, long? odometerKm = null)
        : base(id)
    {
        InspectionNumber = Check.NotNullOrWhiteSpace(inspectionNumber, nameof(inspectionNumber), 32);
        VehicleId = vehicleId;
        OdometerKm = odometerKm;
        Status = InspectionStatus.Booked;
    }

    public void AssignTechnician(Guid technicianUserId)
    {
        TechnicianUserId = technicianUserId;
        if (Status == InspectionStatus.AwaitingTechnician)
        {
            Status = InspectionStatus.Booked;
        }
    }

    public void Start(DateTime startedAt)
    {
        if (Status == InspectionStatus.Completed || Status == InspectionStatus.Cancelled)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.InspectionCannotStart);
        }

        StartedAt = startedAt;
        Status = InspectionStatus.InProgress;
    }

    public InspectionTyreReading AddOrUpdateReading(
        Guid id,
        string position,
        MeasurementSource source,
        decimal? innerTreadMm,
        decimal? centreTreadMm,
        decimal? outerTreadMm,
        decimal? pressurePsi,
        Guid? tyreId = null,
        string? wearPattern = null,
        string? defects = null,
        string? recommendation = null)
    {
        if (Status == InspectionStatus.Completed || Status == InspectionStatus.Cancelled)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.InspectionIsClosed);
        }

        var normalizedPosition = Check.NotNullOrWhiteSpace(position, nameof(position), 32).Trim();
        var reading = _readings.FirstOrDefault(x => x.Position.Equals(normalizedPosition, StringComparison.OrdinalIgnoreCase));

        if (reading is null)
        {
            reading = new InspectionTyreReading(
                id,
                Id,
                normalizedPosition,
                source,
                innerTreadMm,
                centreTreadMm,
                outerTreadMm,
                pressurePsi,
                tyreId,
                wearPattern,
                defects,
                recommendation);

            _readings.Add(reading);
            return reading;
        }

        reading.Update(source, innerTreadMm, centreTreadMm, outerTreadMm, pressurePsi, tyreId, wearPattern, defects, recommendation);
        return reading;
    }

    public void Complete(DateTime completedAt, string? notes = null)
    {
        if (Status != InspectionStatus.InProgress)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.InspectionMustBeInProgress);
        }

        if (_readings.Count == 0)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.InspectionRequiresReading);
        }

        CompletedAt = completedAt;
        Notes = notes?.Trim();
        Status = InspectionStatus.Completed;
    }

    public void Cancel(string? notes = null)
    {
        if (Status == InspectionStatus.Completed)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.CompletedInspectionCannotBeCancelled);
        }

        Notes = notes?.Trim();
        Status = InspectionStatus.Cancelled;
    }
}

public class InspectionTyreReading : Entity<Guid>
{
    public Guid InspectionId { get; private set; }
    public Guid? TyreId { get; private set; }
    public string Position { get; private set; } = string.Empty;
    public MeasurementSource Source { get; private set; }
    public decimal? InnerTreadMm { get; private set; }
    public decimal? CentreTreadMm { get; private set; }
    public decimal? OuterTreadMm { get; private set; }
    public decimal? PressurePsi { get; private set; }
    public string? WearPattern { get; private set; }
    public string? Defects { get; private set; }
    public string? Recommendation { get; private set; }

    protected InspectionTyreReading() { }

    internal InspectionTyreReading(
        Guid id,
        Guid inspectionId,
        string position,
        MeasurementSource source,
        decimal? innerTreadMm,
        decimal? centreTreadMm,
        decimal? outerTreadMm,
        decimal? pressurePsi,
        Guid? tyreId,
        string? wearPattern,
        string? defects,
        string? recommendation)
        : base(id)
    {
        InspectionId = inspectionId;
        Position = position;
        Update(source, innerTreadMm, centreTreadMm, outerTreadMm, pressurePsi, tyreId, wearPattern, defects, recommendation);
    }

    internal void Update(
        MeasurementSource source,
        decimal? innerTreadMm,
        decimal? centreTreadMm,
        decimal? outerTreadMm,
        decimal? pressurePsi,
        Guid? tyreId,
        string? wearPattern,
        string? defects,
        string? recommendation)
    {
        ValidateMeasurement(innerTreadMm, nameof(innerTreadMm));
        ValidateMeasurement(centreTreadMm, nameof(centreTreadMm));
        ValidateMeasurement(outerTreadMm, nameof(outerTreadMm));
        ValidateMeasurement(pressurePsi, nameof(pressurePsi));

        Source = source;
        InnerTreadMm = innerTreadMm;
        CentreTreadMm = centreTreadMm;
        OuterTreadMm = outerTreadMm;
        PressurePsi = pressurePsi;
        TyreId = tyreId;
        WearPattern = wearPattern?.Trim();
        Defects = defects?.Trim();
        Recommendation = recommendation?.Trim();
    }

    private static void ValidateMeasurement(decimal? value, string name)
    {
        if (value < 0)
        {
            throw new BusinessException(TyreLifecycleDomainErrorCodes.InvalidInspectionMeasurement)
                .WithData("Field", name);
        }
    }
}
