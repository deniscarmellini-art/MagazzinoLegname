namespace MagazzinoLegname.Models;

public sealed record ManualPackageRemovalMovement
{
    public Guid MovementId { get; init; } = Guid.NewGuid();
    public required Guid PackageId { get; init; }
    public required string PackageCode { get; init; }
    public required Guid LoadId { get; init; }
    public required Guid MaterialGroupId { get; init; }
    public required string LoadNumber { get; init; }
    public required string SupplierName { get; init; }
    public required DateTime RemovalDate { get; init; }
    public required string RemovalOperator { get; init; }
    public required decimal RemovedCubicMeters { get; init; }
    public required string Reason { get; init; }
    public string Note { get; init; } = string.Empty;
}
