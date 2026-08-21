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

        var legacyReferenceVolume = group.IsLegacyImport
            ? (group.WasClassifiedAtLegacyImport && group.LegacyEstimatedCubicMeters.HasValue
                ? group.LegacyEstimatedCubicMeters.Value
                : group.IncomingPhysicalCubicMeters)
            : group.InitialPieces * group.UsefulThickness * group.FinalWidth * group.FinalLength / 1_000_000_000m;

        var usefulCubicMetersPerBoard = group.InitialPieces == 0 ? 0m : legacyReferenceVolume / group.InitialPieces;
        var theoreticalUseful = legacyReferenceVolume;

        var afterWholeBoards = goodGroupPieces * usefulCubicMetersPerBoard;
        var partialWaste = afterWholeBoards * partialPercentage / 100m;
        var realAvailable = afterWholeBoards - partialWaste;
        var wholeBoardPercentage = group.InitialPieces == 0 ? 0m
            : (decimal)discardedAtGroupLevel / group.InitialPieces * 100m;
        var totalQualityPercentage = theoreticalUseful == 0m ? 0m
            : (theoreticalUseful - realAvailable) / theoreticalUseful * 100m;

        return new(group.InitialPieces, discardedAtGroupLevel, goodGroupPieces,
            usefulCubicMetersPerBoard, theoreticalUseful, afterWholeBoards,
            partialPercentage, partialWaste, realAvailable,
            wholeBoardPercentage, totalQualityPercentage);
    }
}
