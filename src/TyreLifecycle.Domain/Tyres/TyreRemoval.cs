namespace TyreLifecycle.Tyres;

/// <summary>Immutable physical removal; request ID makes retries safe.</summary>
public class TyreRemoval
{
 public Guid Id { get; set; }
 public Guid TyreId { get; set; }
 public Guid VehicleId { get; set; }
 public Guid? ReplacementTyreId { get; set; }
 public Guid ActorId { get; set; }
 public string ActorName { get; set; } = "";
 public string RequestHash { get; set; } = "";
 public string Position { get; set; } = "";
 public string Reason { get; set; } = "";
 public string Destination { get; set; } = "";
 public string? Notes { get; set; }
 public DateOnly RemovalDate { get; set; }
 public long OdometerKm { get; set; }
 public DateTime RecordedAt { get; set; }
}
