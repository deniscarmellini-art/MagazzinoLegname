namespace MagazzinoLegname.Models;

public sealed record LegacyImportBatch(Guid Id, string FileName, string FileFingerprint, DateTime ImportedAt,
    int ImportedRows, int LoadCount, int PackageCount, decimal PhysicalCubicMeters,
    decimal LegacyAvailableCubicMeters, string? ImportOperator);

public sealed record LegacySupplierPlan(string Name, string Code, bool Existing);

public sealed class LegacyInitialInventoryImportPlan
{
    public required Guid BatchId { get; init; }
    public required string FilePath { get; init; }
    public required string FileFingerprint { get; init; }
    public required IReadOnlyList<LegacyStagingRow> Rows { get; init; }
    public required IReadOnlyList<LegacySupplierPlan> Suppliers { get; init; }
    public required IReadOnlyList<string> Collisions { get; init; }
    public int PackageCount => Rows.Count;
    public int LoadCount => Rows.Select(x => $"{x.SupplierNormalized}|{x.LoadNumber}").Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public int ClassifiedCount => Rows.Count(x => x.IsClassified == true);
    public int ToClassifyCount => Rows.Count(x => x.IsClassified != true);
    public decimal PhysicalCubicMeters => Rows.Sum(x => x.RecalculatedPhysicalCubicMeters ?? 0m);
    public decimal LegacyAvailableCubicMeters => Rows.Sum(x => x.LegacyEstimatedCubicMeters ?? 0m);
    public int MissingPrices => Rows.Count;
    public bool CanCommit => Collisions.Count == 0;
}

public sealed record LegacyInitialInventoryImportResult(LegacyImportBatch Batch, int PackagesCreated,
    int LoadsCreated, int SuppliersLinked, int SuppliersCreated, int Classified, int ToClassify,
    decimal PhysicalCubicMeters, decimal LegacyAvailableCubicMeters, int MissingPrices, int SkippedRows);
