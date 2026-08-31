using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class GoodsReceiptCalculationService
{
    private const decimal CubicMillimetersPerCubicMeter = 1_000_000_000m;

    public void Recalculate(GoodsReceiptLine line, SupplierThicknessConfiguration? supplierConfiguration,
        MaterialParameters parameters, decimal appliedPrice)
    {
        line.NotifyDerivedPieceCountsChanged();
        var family = parameters.FindFamily(line.IncomingThickness);
        line.ConventionalThickness = family?.ConventionalThickness ?? 0m;
        line.UsefulProductionThickness = family?.UsefulProductionThickness ?? 0m;
        line.PlaningReduction = supplierConfiguration?.EffectivePlaningReductionMillimeters ?? 0m;
        line.StandardWidthReduction = family?.StandardWidthReductionMillimeters ?? 0m;
        line.FingerJointLengthReduction = family?.FingerJointLengthReductionMillimeters ?? 0m;
        line.FinalWidth = Math.Max(0m, line.IncomingWidth - line.PlaningReduction - line.StandardWidthReduction);
        line.FinalLength = Math.Max(0m, line.IncomingLength - line.FingerJointLengthReduction);

        line.PhysicalIncomingCubicMeters = Volume(line.EnteredPieces, line.IncomingThickness,
            line.IncomingWidth, line.IncomingLength);
        line.TheoreticalUsefulCubicMeters = Volume(line.EnteredPieces, line.UsefulProductionThickness,
            line.FinalWidth, line.FinalLength);
        // Le dimensioni finali restano informazioni tecniche. Lo sfrido di lavorazione
        // non appartiene alla logica di magazzino e non riduce mai la giacenza.
        line.ProcessingLossCubicMeters = 0m;
        line.ProcessingLossPercentage = 0m;

        // Disponibile reale solo dopo classificazione: ricalcolo dimensionale sui pezzi buoni.
        line.RealAvailableUsefulCubicMeters = line.IsClassified
            ? Volume(line.GoodPieces, line.UsefulProductionThickness, line.FinalWidth, line.FinalLength)
            : 0m;
        line.PrezzoApplicato = appliedPrice;
        line.LineValue = line.PhysicalIncomingCubicMeters * line.PrezzoApplicato;
    }

    public static decimal GetConventionalThickness(decimal incomingThickness, MaterialParameters parameters) =>
        parameters.FindFamily(incomingThickness)?.ConventionalThickness ?? 0m;

    private static decimal Volume(int pieces, decimal thickness, decimal width, decimal length) =>
        pieces * thickness * width * length / CubicMillimetersPerCubicMeter;
}
