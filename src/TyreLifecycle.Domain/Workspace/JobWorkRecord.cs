namespace TyreLifecycle.Workspace;
public class JobWorkRecord
{
 public Guid Id { get; set; }
 public Guid VehicleId { get; set; }
 public string JobCardId { get; set; } = "";
 public string ItemId { get; set; } = "";
 public string ItemJson { get; set; } = "{}";
 public string Kind { get; set; } = "";
 public Guid? FitmentId { get; set; }
 public Guid? RotationId { get; set; }
 public string TyresJson { get; set; } = "[]";
 public Guid ActorId { get; set; }
 public string ActorName { get; set; } = "";
 public string RequestHash { get; set; } = "";
 public DateOnly PerformedDate { get; set; }
 public long OdometerKm { get; set; }
 public string Notes { get; set; } = "";
 public DateTime RecordedAt { get; set; }
}
public class CompletedWorkshopJob
{
 public Guid Id { get; set; }
 public string JobCardId { get; set; } = "";
 public Guid VehicleId { get; set; }
 public string SummaryJson { get; set; } = "{}";
 public DateTime CompletedAt { get; set; }
}
