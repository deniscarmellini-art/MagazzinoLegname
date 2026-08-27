namespace MagazzinoLegname.Models;

public sealed record WasteAdjustment
{
    public Guid AdjustmentId { get; init; } = Guid.NewGuid();
    public required Guid LoadId { get; init; }
    public required Guid MaterialGroupId { get; init; }
    public required DateTime AdjustmentDate { get; init; }
    public required string AdjustmentOperator { get; init; }
    public required int InitialPieces { get; init; }
    public required int DiscardedWholeBoards { get; init; }
    public required int GoodPieces { get; init; }
    public required decimal AdjustmentBaseCubicMeters { get; init; }
    public required decimal TheoreticalUsefulCubicMeters { get; init; }
    public required decimal CubicMetersAfterWholeBoardWaste { get; init; }
    public required decimal PartialWastePercentage { get; init; }
    public required decimal PartialWasteCubicMeters { get; init; }
    public required decimal RealAvailableCubicMeters { get; init; }
    public required decimal WholeBoardWastePercentage { get; init; }
    public required decimal TotalClassificationWastePercentage { get; init; }
}
