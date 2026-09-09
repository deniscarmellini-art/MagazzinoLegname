namespace MagazzinoLegname.Persistence.Entities;

public enum PersistentPackageType { Official = 0, Supplementary = 1 }
public enum PackageTerminalEventType { Discharge = 0, Return = 1, ManualRemoval = 2, SupplementaryExit = 3 }

public sealed class LoadEntity
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public SupplierEntity Supplier { get; set; } = null!;
    public string LoadNumber { get; set; } = string.Empty;
    public int? LoadYear { get; set; }
    public int? AnnualProgressive { get; set; }
    public string Certification { get; set; } = string.Empty;
    public DateTime ArrivalDate { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public Guid? ReceiptOperatorId { get; set; }
    public OperatorEntity? ReceiptOperator { get; set; }
    public string? ReceiptOperatorSnapshot { get; set; }
    public int ExpectedPackages { get; set; }
    public string? LegacyLoadNumber { get; set; }
    public Guid? LegacyImportBatchId { get; set; }
    public LegacyImportBatchEntity? LegacyImportBatch { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<MaterialGroupEntity> MaterialGroups { get; set; } = [];
    public ICollection<PackageEntity> Packages { get; set; } = [];
}

public sealed class LoadNumberSequenceEntity
{
    public Guid SupplierId { get; set; }
    public SupplierEntity Supplier { get; set; } = null!;
    public int LoadYear { get; set; }
    public int NextProgressive { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class MaterialGroupEntity
{
    public Guid Id { get; set; }
    public Guid LoadId { get; set; }
    public LoadEntity Load { get; set; } = null!;
    public decimal IncomingThickness { get; set; }
    public decimal ConventionalThickness { get; set; }
    public decimal UsefulThickness { get; set; }
    public decimal IncomingWidth { get; set; }
    public decimal WidthAfterPlaning { get; set; }
    public decimal FinalWidth { get; set; }
    public decimal IncomingLength { get; set; }
    public decimal FinalLength { get; set; }
    public string Quality { get; set; } = string.Empty;
    public int PackageCount { get; set; }
    public int InitialPieces { get; set; }
    public decimal IncomingPhysicalCubicMeters { get; set; }
    public decimal? AppliedPrice { get; set; }
    public decimal? HistoricalValue { get; set; }
    public bool IsClassified { get; set; }
    public bool WasteVerified { get; set; }
    public bool IsLegacyImport { get; set; }
    public decimal? LegacyEstimatedCubicMeters { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<PackageEntity> Packages { get; set; } = [];
    public ICollection<ClassificationMovementEntity> ClassificationMovements { get; set; } = [];
    public ICollection<WasteAdjustmentEntity> WasteAdjustments { get; set; } = [];
}

public sealed class PackageEntity
{
    public Guid Id { get; set; }
    public Guid LoadId { get; set; }
    public LoadEntity Load { get; set; } = null!;
    public Guid MaterialGroupId { get; set; }
    public MaterialGroupEntity MaterialGroup { get; set; } = null!;
    public string PackageCode { get; set; } = string.Empty;
    public string QrPayload { get; set; } = string.Empty;
    public PersistentPackageType PackageType { get; set; }
    public int SequenceNumber { get; set; }
    public int? SupplementarySequence { get; set; }
    public int? PieceCount { get; set; }
    public int TotalOfficialPackages { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal IncomingPhysicalCubicMeters { get; set; }
    public decimal? AppliedPrice { get; set; }
    public decimal? HistoricalPackageValue { get; set; }
    public DateTime ArrivalDate { get; set; }
    public string? LegacyPackageLabel { get; set; }
    public int? LegacyExcelRow { get; set; }
    public string? LegacyQr { get; set; }
    public Guid? LegacyImportBatchId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public PackageTerminalEventEntity? TerminalEvent { get; set; }
}

public sealed class ClassificationMovementEntity
{
    public Guid Id { get; set; }
    public Guid LoadId { get; set; }
    public LoadEntity Load { get; set; } = null!;
    public Guid MaterialGroupId { get; set; }
    public MaterialGroupEntity MaterialGroup { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
    public Guid OperatorId { get; set; }
    public OperatorEntity Operator { get; set; } = null!;
    public string OperatorSnapshot { get; set; } = string.Empty;
}

public sealed class WasteAdjustmentEntity
{
    public Guid Id { get; set; }
    public Guid LoadId { get; set; }
    public LoadEntity Load { get; set; } = null!;
    public Guid MaterialGroupId { get; set; }
    public MaterialGroupEntity MaterialGroup { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
    public Guid OperatorId { get; set; }
    public OperatorEntity Operator { get; set; } = null!;
    public string OperatorSnapshot { get; set; } = string.Empty;
    public int InitialPieces { get; set; }
    public int DiscardedWholeBoards { get; set; }
    public int GoodPieces { get; set; }
    public decimal AdjustmentBaseCubicMeters { get; set; }
    public decimal TheoreticalUsefulCubicMeters { get; set; }
    public decimal CubicMetersAfterWholeBoardWaste { get; set; }
    public decimal PartialWastePercentage { get; set; }
    public decimal PartialWasteCubicMeters { get; set; }
    public decimal RealAvailableCubicMeters { get; set; }
    public decimal WholeBoardWastePercentage { get; set; }
    public decimal TotalClassificationWastePercentage { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class SupplierReturnOperationEntity
{
    public Guid Id { get; set; }
    public Guid LoadId { get; set; }
    public LoadEntity Load { get; set; } = null!;
    public DateTime OccurredAtUtc { get; set; }
    public Guid OperatorId { get; set; }
    public OperatorEntity Operator { get; set; } = null!;
    public string OperatorSnapshot { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? DocumentReference { get; set; }
    public ICollection<PackageTerminalEventEntity> PackageEvents { get; set; } = [];
}

public sealed class PackageTerminalEventEntity
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public PackageEntity Package { get; set; } = null!;
    public PackageTerminalEventType EventType { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid OperatorId { get; set; }
    public OperatorEntity Operator { get; set; } = null!;
    public string OperatorSnapshot { get; set; } = string.Empty;
    public Guid? ReturnOperationId { get; set; }
    public SupplierReturnOperationEntity? ReturnOperation { get; set; }
    public decimal? InventoryCubicMeters { get; set; }
    public decimal? ReturnedPhysicalCubicMeters { get; set; }
    public string? Reason { get; set; }
    public string? Note { get; set; }
}
