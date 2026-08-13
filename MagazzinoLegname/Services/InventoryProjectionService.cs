using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class InventoryProjectionService
{
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;
    private readonly HashSet<string> _removedPackageCodes = new(StringComparer.OrdinalIgnoreCase);

    public void RemovePackage(string packageCode) => _removedPackageCodes.Add(packageCode);

    public IReadOnlyList<InventoryPackage> BuildInventory()
    {
        var packages = new List<InventoryPackage>();
        foreach (var load in _workflow.Loads)
        {
            var loadSequence = 1;
            foreach (var group in load.Groups)
            {
                var adjustment = _workflow.WasteAdjustmentHistory.LastOrDefault(item => item.MaterialGroupId == group.GroupId);
                var groupCubicMeters = adjustment?.RealAvailableCubicMeters ?? group.TheoreticalUsefulCubicMeters;
                var shares = DistributeExactly(groupCubicMeters, group.PackageCount);
                var incomingShares = DistributeExactly(group.IncomingPhysicalCubicMeters, group.PackageCount);
                for (var index = 0; index < group.PackageCount; index++, loadSequence++)
                {
                    packages.Add(new InventoryPackage
                    {
                        LoadId = load.Id, MaterialGroupId = group.GroupId,
                        PackageCode = $"{load.SupplierCode}-{load.LoadNumber.Replace('-', '-')}-P{loadSequence:00}",
                        LoadNumber = load.LoadNumber, SupplierName = load.SupplierName,
                        ArrivalDate = load.ArrivalDate, PackageNumber = loadSequence,
                        TotalPackages = load.TotalPackages, ConventionalThickness = group.ConventionalThickness,
                        WidthAfterPlaning = group.WidthAfterPlaning, IncomingLength = group.IncomingLength,
                        Quality = group.Quality, Certification = load.Certification,
                        ClassificationStatus = group.IsClassified ? "Classificato" : "Da classificare",
                        WasteAdjustmentStatus = adjustment is null ? "—" : "✓",
                        IncomingCubicMeters = incomingShares[index],
                        ProcessingWastePercentage = group.ProcessingWastePercentage,
                        QualityWastePercentage = adjustment?.TotalClassificationWastePercentage,
                        InventoryCubicMeters = shares[index], UsesRealCubicMeters = adjustment is not null,
                        WasteAdjustmentDate = adjustment?.AdjustmentDate,
                        WasteAdjustmentOperator = adjustment?.AdjustmentOperator
                    });
                }
            }
        }
        return packages.Where(package => package.IsPresent
            && !_removedPackageCodes.Contains(package.PackageCode)).ToList();
    }

    public static IReadOnlyList<decimal> DistributeExactly(decimal total, int count)
    {
        if (count <= 0) return [];
        var result = new decimal[count];
        var regularShare = decimal.Round(total / count, 6, MidpointRounding.ToEven);
        for (var index = 0; index < count - 1; index++) result[index] = regularShare;
        result[count - 1] = total - result.Take(count - 1).Sum();
        return result;
    }
}
