namespace MagazzinoLegname.Models;

public sealed record ClassificationMovement
{
    public Guid MovementId { get; init; } = Guid.NewGuid();
    public required Guid LoadId { get; init; }
    public required Guid MaterialGroupId { get; init; }
    public required DateTime ClassificationDate { get; init; }
    public required string ClassificationOperator { get; init; }
}
