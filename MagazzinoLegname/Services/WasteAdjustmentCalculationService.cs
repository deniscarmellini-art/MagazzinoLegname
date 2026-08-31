using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class WasteAdjustmentCalculationService
{
    public WasteAdjustmentCalculation Calculate(decimal incomingPhysicalCubicMeters, int initialPieces,
        int discardedWholeBoards, decimal partialWastePercentage)
    {
        var discardedAtGroupLevel = Math.Clamp(discardedWholeBoards, 0, initialPieces);
        var partialPercentage = Math.Clamp(partialWastePercentage, 0m, 100m);
        var goodGroupPieces = initialPieces - discardedAtGroupLevel;

        var adjustmentBaseCubicMeters = incomingPhysicalCubicMeters;
        var usefulCubicMetersPerBoard = initialPieces == 0
            ? 0m
            : adjustmentBaseCubicMeters / initialPieces;

        var afterWholeBoards = goodGroupPieces * usefulCubicMetersPerBoard;
        var partialWaste = afterWholeBoards * partialPercentage / 100m;
        var realAvailable = afterWholeBoards - partialWaste;
        var wholeBoardPercentage = initialPieces == 0 ? 0m
            : (decimal)discardedAtGroupLevel / initialPieces * 100m;
        var totalQualityPercentage = adjustmentBaseCubicMeters == 0m ? 0m
            : (adjustmentBaseCubicMeters - realAvailable) / adjustmentBaseCubicMeters * 100m;

        return new(initialPieces, discardedAtGroupLevel, goodGroupPieces,
            usefulCubicMetersPerBoard, adjustmentBaseCubicMeters, afterWholeBoards,
            partialPercentage, partialWaste, realAvailable,
            wholeBoardPercentage, totalQualityPercentage);
    }
}
