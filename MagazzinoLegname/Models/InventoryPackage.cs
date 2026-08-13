namespace MagazzinoLegname.Models;

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
    public bool UsesRealCubicMeters { get; init; }
    public bool IsPresent { get; set; } = true;
    public DateTime? WasteAdjustmentDate { get; init; }
    public string? WasteAdjustmentOperator { get; init; }
    public string PackagePosition => $"{PackageNumber} / {TotalPackages}";
    public string QualityWasteDisplay => QualityWastePercentage.HasValue
        ? $"{QualityWastePercentage.Value:N2}%" : "—";
    public string OperationalMeasure => $"{ConventionalThickness:0} × {WidthAfterPlaning:0} × {IncomingLength:0}";
    public string InventoryStatus => ClassificationStatus switch
    {
        "Da classificare" => "Da classificare",
        _ when !UsesRealCubicMeters => "Classificato · rettifica da fare",
        _ => "Disponibile"
    };
}
