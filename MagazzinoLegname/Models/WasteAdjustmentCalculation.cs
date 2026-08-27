namespace MagazzinoLegname.Models;

public sealed record WasteAdjustmentCalculation(
    int InitialGroupPieces,
    int DiscardedWholeBoards,
    int GoodGroupPieces,
    decimal UsefulCubicMetersPerBoard,
    decimal AdjustmentBaseCubicMeters,
    decimal CubicMetersAfterWholeBoardWaste,
    decimal PartialWastePercentage,
    decimal PartialWasteCubicMeters,
    decimal RealAvailableCubicMeters,
    decimal WholeBoardWastePercentage,
    decimal TotalQualityWastePercentage);
