namespace TyreLifecycle.Inspections;
/// <summary>Immutable receipt for a completed workshop visit and its retry key.</summary>
public class WorkshopInspection
{
 public Guid Id { get; set; }
 public Guid VehicleId { get; set; }
 public Guid ActorId { get; set; }
 public string RequestHash { get; set; } = "";
 public string InspectionIdsJson { get; set; } = "[]";
 public string? JobCardId { get; set; }
 public DateOnly InspectionDate { get; set; }
 public DateTime RecordedAt { get; set; }
}
