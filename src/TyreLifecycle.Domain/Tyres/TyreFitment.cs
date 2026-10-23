namespace TyreLifecycle.Tyres;

/// <summary>Immutable record of a completed fitment. Id is the caller's retry key.</summary>
public class TyreFitment
{
    public Guid Id { get; set; }
    public Guid TyreId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string BranchId { get; set; } = "";
    public string Position { get; set; } = "";
    public DateOnly FitmentDate { get; set; }
    public long OdometerKm { get; set; }
    public decimal? InitialTreadMm { get; set; }
    public string? Notes { get; set; }
    public string? StockSku { get; set; }
    public DateTime RecordedAt { get; set; }
}
