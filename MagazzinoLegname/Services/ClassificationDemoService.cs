using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class ClassificationDemoService
{
    public IReadOnlyList<ClassificationLoad> CreateLoads()
    {
        var mixedLoadId = Guid.NewGuid();
        var secondLoadId = Guid.NewGuid();
        var classifiedLoadId = Guid.NewGuid();
        var classifiedGroups = new[]
        {
            Group(classifiedLoadId, 46, 44, 40, 180, 175, 165, 4000, 3900, "C", 8, 1200),
            Group(classifiedLoadId, 36, 34, 30, 180, 180, 170, 4000, 3900, "VISTA", 4, 600)
        };
        foreach (var group in classifiedGroups)
            group.MarkAsClassified("Elena Bianchi", DateTime.Today.AddHours(8));
        return
        [
            new ClassificationLoad(
            [
                Group(mixedLoadId, 46, 44, 40, 180, 175, 165, 4000, 3900, "C", 8, 1200),
                Group(mixedLoadId, 36, 34, 30, 180, 180, 170, 4000, 3900, "C", 4, 600)
            ]) { Id = mixedLoadId, LoadNumber = "3-26", SupplierName = "Segheria Alpina S.r.l.", SupplierCode = "SEG", ArrivalDate = DateTime.Today.AddDays(-1) },
            new ClassificationLoad(
            [
                Group(secondLoadId, 25, 23, 20, 160, 155, 145, 3000, 2900, "VISTA", 6, 720),
                Group(secondLoadId, 34, 34, 30, 200, 195, 185, 4000, 3900, "C", 3, 360)
            ]) { Id = secondLoadId, LoadNumber = "2-26", SupplierName = "Legnami Nord S.p.A.", SupplierCode = "LEG", ArrivalDate = DateTime.Today.AddDays(-3) },
            new ClassificationLoad(classifiedGroups)
                { Id = classifiedLoadId, LoadNumber = "1-26", SupplierName = "Bosco & Tavole S.r.l.", SupplierCode = "BET", ArrivalDate = DateTime.Today.AddDays(-5) }
        ];
    }

    private static MaterialGroupClassification Group(Guid loadId, decimal incomingThickness,
        decimal conventionalThickness, decimal usefulThickness, decimal incomingWidth,
        decimal widthAfterPlaning, decimal finalWidth, decimal length, decimal finalLength,
        string quality, int packages, int pieces) => new()
        {
            LoadId = loadId, IncomingThickness = incomingThickness,
            ConventionalThickness = conventionalThickness, UsefulThickness = usefulThickness,
            IncomingWidth = incomingWidth, WidthAfterPlaning = widthAfterPlaning,
            FinalWidth = finalWidth, IncomingLength = length, FinalLength = finalLength,
            Quality = quality, PackageCount = packages, InitialPieces = pieces
        };
}
