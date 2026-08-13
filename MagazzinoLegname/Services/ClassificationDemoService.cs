using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class ClassificationDemoService
{
    public IReadOnlyList<ClassificationLoad> CreateLoads()
    {
        var mixedLoadId = Guid.NewGuid();
        var secondLoadId = Guid.NewGuid();
        return
        [
            new ClassificationLoad(
            [
                Group(mixedLoadId, 46, 44, 40, 180, 175, 4000, "C", 8, 1200),
                Group(mixedLoadId, 36, 34, 30, 180, 180, 4000, "C", 4, 600)
            ]) { Id = mixedLoadId, LoadNumber = "3-26", SupplierName = "Segheria Alpina S.r.l.", ArrivalDate = DateTime.Today.AddDays(-1) },
            new ClassificationLoad(
            [
                Group(secondLoadId, 25, 23, 20, 160, 155, 3000, "VISTA", 6, 720),
                Group(secondLoadId, 34, 34, 30, 200, 195, 4000, "C", 3, 360)
            ]) { Id = secondLoadId, LoadNumber = "2-26", SupplierName = "Legnami Nord S.p.A.", ArrivalDate = DateTime.Today.AddDays(-3) }
        ];
    }

    private static MaterialGroupClassification Group(Guid loadId, decimal incomingThickness,
        decimal conventionalThickness, decimal usefulThickness, decimal incomingWidth,
        decimal widthAfterPlaning, decimal length, string quality, int packages, int pieces) => new()
        {
            LoadId = loadId, IncomingThickness = incomingThickness,
            ConventionalThickness = conventionalThickness, UsefulThickness = usefulThickness,
            IncomingWidth = incomingWidth, WidthAfterPlaning = widthAfterPlaning,
            IncomingLength = length, Quality = quality, PackageCount = packages, InitialPieces = pieces
        };
}
