using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class PackageExpansionService
{
    public IReadOnlyList<PhysicalPackageDraft> Expand(GoodsReceiptLoadDraft load,
        DateTime arrivalDate, IEnumerable<GoodsReceiptLine> groups)
    {
        var packages = new List<PhysicalPackageDraft>();
        var sequence = 1;
        foreach (var group in groups)
        {
            var expanded = group.ExpandToPhysicalPackages(load.Id, sequence)
                .Select(package =>
                {
                    var code = $"{load.SupplierCodeApplied}-{load.AnnualSequence}-{load.Year % 100:00}-P{package.SequenceNumber:00}";
                    return package with { PackageCode = code };
                }).ToList();
            packages.AddRange(expanded);
            sequence += expanded.Count;
        }
        var totalPackages = packages.Count;
        packages = packages.Select(package =>
        {
            var withStableData = package with { TotalPackages = totalPackages, ArrivalDate = arrivalDate.Date };
            var payload = new QrCodeService().BuildPayload(withStableData);
            return withStableData with { QrPayload = payload };
        }).ToList();
        if (packages.Select(package => package.PackageCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != packages.Count)
            throw new InvalidOperationException("Sono stati generati codici pacco duplicati.");
        // Il database futuro dovrà applicare anche un vincolo UNIQUE su PackageCode.
        return packages;
    }
}
