namespace TyreLifecycle.Tyres;
public class TyreRotation
{
 public Guid Id { get; set; }
 public Guid VehicleId { get; set; }
 public Guid ActorId { get; set; }
 public string ActorName { get; set; } = "";
 public string RequestHash { get; set; } = "";
 public string MovesJson { get; set; } = "[]";
 public DateOnly RotationDate { get; set; }
 public long OdometerKm { get; set; }
 public string? Notes { get; set; }
 public DateTime RecordedAt { get; set; }
}
