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
}
