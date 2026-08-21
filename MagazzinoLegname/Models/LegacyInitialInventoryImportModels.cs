namespace MagazzinoLegname.Models;

public sealed record LegacyImportBatch(Guid Id, string FileName, string FileFingerprint, DateTime ImportedAt,
    int ImportedRows, int LoadCount, int PackageCount, decimal PhysicalCubicMeters,
    decimal LegacyAvailableCubicMeters, string? ImportOperator);

public sealed record LegacySupplierPlan(string Name, string Code, bool Existing);
public sealed record LegacyMaterialGroupPreview(string LoadNumber, int GroupNumber, decimal IncomingThickness,
    decimal IncomingWidth, decimal IncomingLength, string Quality, string ClassificationStatus,
    string Certification, int PackageCount, int InitialPieces, decimal PhysicalCubicMeters,
    decimal LegacyEstimatedCubicMeters);

public sealed class LegacyInitialInventoryImportPlan
{
    public required Guid BatchId { get; init; }
    public required string FilePath { get; init; }
    public required string FileFingerprint { get; init; }
    public required IReadOnlyList<LegacyStagingRow> Rows { get; init; }
    public required IReadOnlyList<LegacySupplierPlan> Suppliers { get; init; }
    public required IReadOnlyList<string> Collisions { get; init; }
    public required IReadOnlyList<LegacyMaterialGroupPreview> MaterialGroups { get; init; }
    public int PackageCount => Rows.Count;
    public int LoadCount => Rows.Select(x => $"{x.SupplierNormalized}|{x.LoadNumber}").Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public int ClassifiedCount => Rows.Count(x => x.IsClassified == true);
    public int ToClassifyCount => Rows.Count(x => x.IsClassified != true);
    public decimal PhysicalCubicMeters => Rows.Sum(x => x.RecalculatedPhysicalCubicMeters ?? 0m);
    public decimal LegacyAvailableCubicMeters => Rows.Sum(x => x.LegacyEstimatedCubicMeters ?? 0m);
    public int MissingPrices => Rows.Count;
    public int MaterialGroupCount => MaterialGroups.Count;
    public int ClassifiedMaterialGroups => MaterialGroups.Count(x => x.ClassificationStatus == "Classificato");
    public int MaterialGroupsToClassify => MaterialGroups.Count(x => x.ClassificationStatus == "Da classificare");
    public decimal AveragePackagesPerGroup => MaterialGroupCount == 0 ? 0m : (decimal)PackageCount / MaterialGroupCount;
    public int MaximumPackagesPerGroup => MaterialGroups.Count == 0 ? 0 : MaterialGroups.Max(x => x.PackageCount);
    public bool CanCommit => Collisions.Count == 0;
}

public sealed record LegacyInitialInventoryImportResult(LegacyImportBatch Batch, int PackagesCreated,
    int LoadsCreated, int SuppliersLinked, int SuppliersCreated, int Classified, int ToClassify,
    decimal PhysicalCubicMeters, decimal LegacyAvailableCubicMeters, int MissingPrices, int SkippedRows);
