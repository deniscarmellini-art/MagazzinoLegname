namespace MagazzinoLegname.Models;

public sealed record SupplementaryPackage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid LoadId { get; init; }
    public required Guid MaterialGroupId { get; init; }
    public required string PackageCode { get; init; }
    public required string QrPayload { get; init; }
    public required int SupplementarySequence { get; init; }
    public required string SupplierName { get; init; }
    public required string SupplierCode { get; init; }
    public required string LoadNumber { get; init; }
    public required DateTime ArrivalDate { get; init; }
    public required decimal IncomingThickness { get; init; }
    public required decimal ConventionalThickness { get; init; }
    public required decimal IncomingWidth { get; init; }
    public required decimal WidthAfterPlaning { get; init; }
    public required decimal IncomingLength { get; init; }
    public required string Quality { get; init; }
    public required string Certification { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string CreatedBy { get; init; }
    public string SupplementaryCode => $"S{SupplementarySequence:00}";
}