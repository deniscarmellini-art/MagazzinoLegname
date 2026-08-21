namespace MagazzinoLegname.Models;

public enum InventoryQuantitySource { CurrentTheoretical, LegacyEstimate, RealAfterAdjustment }

public sealed class InventoryPackage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid LoadId { get; init; }
    public required Guid MaterialGroupId { get; init; }
    public required string PackageCode { get; init; }
    public required string LoadNumber { get; init; }
    public required string SupplierName { get; init; }
    public DateTime ArrivalDate { get; init; }
    public int PackageNumber { get; init; }
    public int TotalPackages { get; init; }
    public decimal ConventionalThickness { get; init; }
    public decimal IncomingThickness { get; init; }
    public decimal WidthAfterPlaning { get; init; }
    public decimal IncomingLength { get; init; }
    public required string Quality { get; init; }
    public required string Certification { get; init; }
    public required string ClassificationStatus { get; init; }
    public required string WasteAdjustmentStatus { get; init; }
    public decimal IncomingCubicMeters { get; init; }
    public decimal ProcessingWastePercentage { get; init; }
    public decimal? QualityWastePercentage { get; init; }
    public decimal InventoryCubicMeters { get; init; }
    public decimal? AppliedPrice { get; init; }
    public decimal? PackageValue => AppliedPrice.HasValue ? InventoryCubicMeters * AppliedPrice.Value : null;
    public decimal? TheoreticalUsefulCubicMeters { get; init; }
    public decimal? LegacyEstimatedCubicMeters { get; init; }
    public InventoryQuantitySource InventoryQuantitySource { get; init; }
    public string? LegacyLoadNumber { get; init; }
    public string? LegacyPackageLabel { get; init; }
    public int? LegacyExcelRow { get; init; }
    public string? LegacyQr { get; init; }
    public Guid? LegacyImportBatchId { get; init; }
    public string PriceDisplay => AppliedPrice.HasValue ? $"{AppliedPrice:N2} €/m³" : "N/D";
    public string PackageValueDisplay => PackageValue.HasValue ? $"{PackageValue:N2} €" : "N/D";
    public bool UsesRealCubicMeters { get; init; }
    public bool IsPresent { get; set; } = true;
    public string PackageStatus { get; init; } = "Presente";
    public DateTime? DischargeDate { get; init; }
    public string? DischargeOperator { get; init; }
    public decimal? DischargedCubicMeters { get; init; }
    public DateTime? ManualRemovalDate { get; init; }
    public string? ManualRemovalOperator { get; init; }
    public decimal? ManuallyRemovedCubicMeters { get; init; }
    public string? ManualRemovalReason { get; init; }
    public string? ManualRemovalNote { get; init; }
    public DateTime? WasteAdjustmentDate { get; init; }
    public string? WasteAdjustmentOperator { get; init; }
    public string PackagePosition => $"{PackageNumber} / {TotalPackages}";
    public string QualityWasteDisplay => QualityWastePercentage.HasValue
        ? $"{QualityWastePercentage.Value:N2}%" : "—";
    public string OperationalMeasure => $"{ConventionalThickness:N2} × {WidthAfterPlaning:N2} × {IncomingLength:N2}";
    public string InventoryStatus => ClassificationStatus switch
    {
        "Da classificare" => "Da classificare",
        _ when !UsesRealCubicMeters => "Classificato · rettifica da fare",
        _ => "Disponibile"
    };
}
