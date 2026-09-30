namespace MagazzinoLegname.Models;

// A detached SQL cell (or an unsaved empty draft), expressed in cubic metres.
public sealed class PlannedConsumption
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required DateTime WeekStart { get; init; }
    public required decimal ConventionalThickness { get; init; }
    public required string Quality { get; init; }
    public decimal ExpectedCubicMeters { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
    public PlannedConsumption Copy() => new() { Id = Id, WeekStart = WeekStart,
        ConventionalThickness = ConventionalThickness, Quality = Quality,
        ExpectedCubicMeters = ExpectedCubicMeters, CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc, RowVersion = RowVersion.ToArray() };
}
