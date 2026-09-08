namespace MagazzinoLegname.Persistence.Entities;

public enum LegacyImportKind { InitialInventory = 0, ClosedHistory = 1 }

public sealed class LegacyImportBatchEntity
{
    public Guid Id { get; set; }
    public LegacyImportKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileFingerprint { get; set; } = string.Empty;
    public DateTime ImportedAtUtc { get; set; }
    public Guid? ImportedByOperatorId { get; set; }
    public OperatorEntity? ImportedByOperator { get; set; }
    public string? ImportedBySnapshot { get; set; }
    public int ImportedRows { get; set; }
    public int LoadCount { get; set; }
    public int PackageCount { get; set; }
    public decimal PhysicalCubicMeters { get; set; }
    public decimal LegacyAvailableCubicMeters { get; set; }
    public ICollection<LegacyImportKeyEntity> ImportKeys { get; set; } = [];
    public ICollection<LegacyHistoricalRecordEntity> HistoricalRecords { get; set; } = [];
}

public sealed class LegacyImportKeyEntity
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public LegacyImportBatchEntity Batch { get; set; } = null!;
    public string ImportKey { get; set; } = string.Empty;
    public int ExcelRow { get; set; }
}

public sealed class LegacyHistoricalRecordEntity
{
    public Guid Id { get; set; }
    public Guid HistoricalLoadId { get; set; }
    public Guid BatchId { get; set; }
    public LegacyImportBatchEntity Batch { get; set; } = null!;
    public string ImportKey { get; set; } = string.Empty;
    public int ExcelRow { get; set; }
    public string SupplierNameSnapshot { get; set; } = string.Empty;
    public DateTime ArrivalDate { get; set; }
    public string LoadNumber { get; set; } = string.Empty;
    public string? PackageLabel { get; set; }
    public decimal Pieces { get; set; }
    public decimal IncomingThickness { get; set; }
    public decimal IncomingWidth { get; set; }
    public decimal IncomingLength { get; set; }
    public string? QualityOriginal { get; set; }
    public string? QualityNormalized { get; set; }
    public string? Certification { get; set; }
    public decimal PhysicalCubicMeters { get; set; }
    public decimal LegacyAvailableCubicMeters { get; set; }
    public decimal? LegacyEstimatedCubicMeters { get; set; }
    public bool? IsClassified { get; set; }
    public DateTime? ClassificationDate { get; set; }
    public string? FinishedRawValue { get; set; }
    public DateTime? FinishedOn { get; set; }
    public string? LegacyQr { get; set; }
}
