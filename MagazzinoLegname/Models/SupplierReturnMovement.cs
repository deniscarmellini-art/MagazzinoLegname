namespace MagazzinoLegname.Models;

public enum SupplierReturnMode { Partial, Total }

public sealed record SupplierReturnMovement
{
    public Guid MovementId { get; init; } = Guid.NewGuid();
    public required Guid ReturnOperationId { get; init; }
    public required Guid PackageId { get; init; }
    public required string PackageCode { get; init; }
    public required Guid LoadId { get; init; }
    public required Guid MaterialGroupId { get; init; }
    public required string LoadNumber { get; init; }
    public required string SupplierName { get; init; }
    public required DateTime ReturnDate { get; init; }
    public required string ReturnOperator { get; init; }
    public required string Reason { get; init; }
    public string Note { get; init; } = string.Empty;
    public string DocumentReference { get; init; } = string.Empty;
    public required decimal ReturnedPhysicalCubicMeters { get; init; }
    public required decimal RemovedInventoryCubicMeters { get; init; }
    public required SupplierReturnMode Mode { get; init; }
}

public sealed record SupplierReturnResult(Guid OperationId, SupplierReturnMode Mode,
    int ReturnedPackages, decimal ReturnedPhysicalCubicMeters, decimal RemovedInventoryCubicMeters);
