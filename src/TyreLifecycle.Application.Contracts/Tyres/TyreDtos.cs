using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace TyreLifecycle.Tyres;

public class TyreDto : FullAuditedEntityDto<Guid>
{
    public Guid VehicleId { get; set; }
    public string TyreNumber { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public decimal? TreadDepthMm { get; set; }
    public TyreHealthStatus HealthStatus { get; set; }
    public long FitmentOdometerKm { get; set; }
    public bool IsActive { get; set; }
}

public class CreateTyreDto
{
    [Required] public Guid VehicleId { get; set; }
    [Required, StringLength(64)] public string TyreNumber { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Brand { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Model { get; set; } = string.Empty;
    [Required, StringLength(64)] public string Size { get; set; } = string.Empty;
    [Required, StringLength(32)] public string Position { get; set; } = string.Empty;
    [Range(0, long.MaxValue)] public long FitmentOdometerKm { get; set; }
}

public class RecordTreadDto
{
    [Range(0, 30)] public decimal TreadDepthMm { get; set; }
    public TyreHealthStatus HealthStatus { get; set; }
}

public class MoveTyreDto
{
    [Required, StringLength(32)] public string Position { get; set; } = string.Empty;
}
