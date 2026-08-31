namespace MagazzinoLegname.Models;

public sealed record SupplementaryPackageExitMovement
{
    public Guid MovementId { get; init; } = Guid.NewGuid();
    public required Guid PackageId { get; init; }
    public required string PackageCode { get; init; }
    public required Guid LoadId { get; init; }
    public required Guid MaterialGroupId { get; init; }
    public required string LoadNumber { get; init; }
    public required string SupplierName { get; init; }
    public required DateTime ExitDate { get; init; }
    public required string ExitOperator { get; init; }
}