namespace MagazzinoLegname.Models;

public sealed record MaterialDischargeMovement
{
    public Guid MovementId { get; init; } = Guid.NewGuid();
    public required Guid PackageId { get; init; }
    public required string PackageCode { get; init; }
    public required Guid LoadId { get; init; }
    public required Guid MaterialGroupId { get; init; }
    public required string LoadNumber { get; init; }
    public required string SupplierName { get; init; }
    public required DateTime DischargeDate { get; init; }
    public required string DischargeOperator { get; init; }
    public required decimal DischargedCubicMeters { get; init; }
    public required string PreviousStatus { get; init; }
    public required string NextStatus { get; init; }
}
