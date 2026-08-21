namespace MagazzinoLegname.Models;

public sealed record PhysicalPackageDraft(
    Guid Id,
    Guid LoadId,
    Guid OriginGroupId,
    int SequenceNumber,
    int PieceCount,
    decimal IncomingThickness,
    decimal IncomingWidth,
    decimal WidthAfterPlaning,
    decimal IncomingLength,
    string Quality)
{
    public required int TotalPackages { get; init; }
    public required DateTime ArrivalDate { get; init; }
    public string Status { get; init; } = "Da classificare";
    public required string PackageCode { get; init; }
    public required string QrPayload { get; init; }
    public string? LegacyPackageLabel { get; init; }
    public int? LegacyExcelRow { get; init; }
    public string? LegacyQr { get; init; }
    public string? LegacyIdentifier { get; init; }
    public decimal? LegacyEstimatedCubicMeters { get; init; }
    public Guid? LegacyImportBatchId { get; init; }
    public int? LegacyPackageNumber { get; init; }
    public int? LegacyTotalPackages { get; init; }
    public decimal IncomingPhysicalCubicMeters => PieceCount * IncomingThickness * IncomingWidth * IncomingLength / 1_000_000_000m;
}
