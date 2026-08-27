using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class WasteAdjustmentCalculationService
{
    public WasteAdjustmentCalculation Calculate(MaterialGroupClassification group,
        int discardedWholeBoards, decimal partialWastePercentage)
    {
        var discardedAtGroupLevel = Math.Clamp(discardedWholeBoards, 0, group.InitialPieces);
        var partialPercentage = Math.Clamp(partialWastePercentage, 0m, 100m);
        var goodGroupPieces = group.InitialPieces - discardedAtGroupLevel;

        var adjustmentBaseCubicMeters = group.AdjustmentBaseCubicMeters;
        var usefulCubicMetersPerBoard = group.InitialPieces == 0
            ? 0m
            : adjustmentBaseCubicMeters / group.InitialPieces;

        var afterWholeBoards = goodGroupPieces * usefulCubicMetersPerBoard;
        var partialWaste = afterWholeBoards * partialPercentage / 100m;
        var realAvailable = afterWholeBoards - partialWaste;
        var wholeBoardPercentage = group.InitialPieces == 0 ? 0m
            : (decimal)discardedAtGroupLevel / group.InitialPieces * 100m;
        var totalQualityPercentage = adjustmentBaseCubicMeters == 0m ? 0m
            : (adjustmentBaseCubicMeters - realAvailable) / adjustmentBaseCubicMeters * 100m;

        return new(group.InitialPieces, discardedAtGroupLevel, goodGroupPieces,
            usefulCubicMetersPerBoard, adjustmentBaseCubicMeters, afterWholeBoards,
            partialPercentage, partialWaste, realAvailable,
            wholeBoardPercentage, totalQualityPercentage);
    }
}
