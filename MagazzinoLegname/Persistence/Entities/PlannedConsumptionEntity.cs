namespace MagazzinoLegname.Persistence.Entities;
public sealed class PlannedConsumptionEntity
{
    public Guid Id { get; set; }
    public DateTime WeekStart { get; set; }
    public decimal ConventionalThickness { get; set; }
    public string Quality { get; set; } = string.Empty;
    public decimal ExpectedCubicMeters { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
