using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace TyreLifecycle.Vehicles;

public class VehicleDto : FullAuditedEntityDto<Guid>
{
    public Guid CustomerId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public long OdometerKm { get; set; }
    public string? Vin { get; set; }
    public bool IsActive { get; set; }
}

public class CreateVehicleDto
{
    [Required] public Guid CustomerId { get; set; }
    [Required, StringLength(32)] public string RegistrationNumber { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Make { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Model { get; set; } = string.Empty;
    [Range(1900, 2100)] public int Year { get; set; }
    [Range(0, long.MaxValue)] public long OdometerKm { get; set; }
    [StringLength(64)] public string? Vin { get; set; }
}

public class UpdateVehicleDto
{
    [Range(0, long.MaxValue)] public long OdometerKm { get; set; }
    public bool IsActive { get; set; }
}
