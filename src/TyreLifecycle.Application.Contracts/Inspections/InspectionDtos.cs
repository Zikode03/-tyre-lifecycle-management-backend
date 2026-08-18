using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace TyreLifecycle.Inspections;

public class InspectionTyreReadingDto : EntityDto<Guid>
{
    public Guid InspectionId { get; set; }
    public Guid? TyreId { get; set; }
    public string Position { get; set; } = string.Empty;
    public MeasurementSource Source { get; set; }
    public decimal? InnerTreadMm { get; set; }
    public decimal? CentreTreadMm { get; set; }
    public decimal? OuterTreadMm { get; set; }
    public decimal? PressurePsi { get; set; }
    public string? WearPattern { get; set; }
    public string? Defects { get; set; }
    public string? Recommendation { get; set; }
}

public class InspectionDto : FullAuditedEntityDto<Guid>
{
    public string InspectionNumber { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public Guid? TechnicianUserId { get; set; }
    public long? OdometerKm { get; set; }
    public InspectionStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public List<InspectionTyreReadingDto> Readings { get; set; } = [];
}

public class CreateInspectionDto
{
    [Required, StringLength(32)]
    public string InspectionNumber { get; set; } = string.Empty;

    public Guid VehicleId { get; set; }

    [Range(0, long.MaxValue)]
    public long? OdometerKm { get; set; }
}

public class AssignInspectionTechnicianDto
{
    public Guid TechnicianUserId { get; set; }
}

public class AddOrUpdateInspectionReadingDto
{
    public Guid? TyreId { get; set; }

    [Required, StringLength(32)]
    public string Position { get; set; } = string.Empty;

    public MeasurementSource Source { get; set; }

    [Range(0, 30)] public decimal? InnerTreadMm { get; set; }
    [Range(0, 30)] public decimal? CentreTreadMm { get; set; }
    [Range(0, 30)] public decimal? OuterTreadMm { get; set; }
    [Range(0, 200)] public decimal? PressurePsi { get; set; }

    [StringLength(128)] public string? WearPattern { get; set; }
    [StringLength(1000)] public string? Defects { get; set; }
    [StringLength(1000)] public string? Recommendation { get; set; }
}

public class CompleteInspectionDto
{
    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class CancelInspectionDto
{
    [StringLength(2000)]
    public string? Notes { get; set; }
}
