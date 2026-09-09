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
        line.PlaningReduction = supplierConfiguration?.EffectivePlaningReductionMillimeters ?? 0m;

        line.PhysicalIncomingCubicMeters = Volume(line.EnteredPieces, line.IncomingThickness,
            line.IncomingWidth, line.IncomingLength);
        line.PrezzoApplicato = appliedPrice;
        line.LineValue = line.PhysicalIncomingCubicMeters * line.PrezzoApplicato;
    }

    public static decimal GetConventionalThickness(decimal incomingThickness, MaterialParameters parameters) =>
        parameters.FindFamily(incomingThickness)?.ConventionalThickness ?? 0m;

    private static decimal Volume(int pieces, decimal thickness, decimal width, decimal length) =>
        pieces * thickness * width * length / CubicMillimetersPerCubicMeter;
}
