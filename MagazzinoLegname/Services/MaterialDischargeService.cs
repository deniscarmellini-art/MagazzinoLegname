using System.Text.RegularExpressions;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed partial class MaterialDischargeService
{
    private readonly InventoryProjectionService _inventory = InventoryProjectionService.Shared;

    public PackageLookupResult Lookup(string qrPayload)
    {
        if (!TryExtractPackageCode(qrPayload, out var packageCode))
            return new(PackageLookupStatus.InvalidQr, "QR non valido");

        var package = _inventory.FindPackage(packageCode);
        if (package is null) return new(PackageLookupStatus.NotFound, "Pacco non trovato");
        var previousMovement = _inventory.FindMovement(packageCode);
        if (previousMovement is not null || !package.IsPresent)
            return new(PackageLookupStatus.AlreadyDischarged, "Pacco già scaricato", package, previousMovement);
        if (package.ClassificationStatus != "Classificato")
            return new(PackageLookupStatus.NotClassified,
                "Scarico non consentito. Il materiale deve essere classificato prima dello scarico.", package);
        if (!package.UsesRealCubicMeters)
            return new(PackageLookupStatus.WasteAdjustmentRequired,
                "Scarico non consentito. È necessario completare la rettifica scarti.", package);
        return new(PackageLookupStatus.Ready, "Pacco pronto per lo scarico", package);
    }

    public MaterialDischargeMovement Confirm(InventoryPackage package, string operatorName) =>
        _inventory.Discharge(package.PackageCode, operatorName);

    private static bool TryExtractPackageCode(string payload, out string packageCode)
    {
        packageCode = string.Empty;
        if (string.IsNullOrWhiteSpace(payload)) return false;
        var scannedValue = payload.Trim();
        var normalizedValue = scannedValue.ToUpperInvariant();
        if (PackageCodePattern().IsMatch(normalizedValue))
        {
            packageCode = normalizedValue;
            return true;
        }
        var idPart = scannedValue.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith("ID=", StringComparison.OrdinalIgnoreCase));
        if (idPart is null) return false;
        packageCode = idPart[3..].Trim().ToUpperInvariant();
        return PackageCodePattern().IsMatch(packageCode);
    }

    [GeneratedRegex("^[A-Z0-9]{2,8}-[0-9]+-[0-9]{2}-P[0-9]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageCodePattern();
}
