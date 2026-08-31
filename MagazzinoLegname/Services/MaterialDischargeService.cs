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
        if (package.IsSupplementary)
        {
            var previousExit = _inventory.FindSupplementaryExit(packageCode);
            if (previousExit is not null || !package.IsPresent)
                return new(PackageLookupStatus.AlreadyDischarged,
                    "Pacco supplementare già uscito dalla giacenza fisica.", package);
            return new(PackageLookupStatus.Ready,
                "Pacco supplementare riconosciuto. Verrà registrata l'uscita fisica senza movimento di MC.", package);
        }

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

    public PackageExitResult Confirm(InventoryPackage package, string operatorName)
    {
        if (package.IsSupplementary)
        {
            var movement = _inventory.ExitSupplementaryPackage(package.PackageCode, operatorName);
            return new PackageExitResult(movement.PackageCode, PackageType.Supplementary, movement.ExitDate,
                movement.ExitOperator, null, "Uscita supplementare registrata senza movimento di MC.");
        }

        var discharge = _inventory.Discharge(package.PackageCode, operatorName);
        return new PackageExitResult(discharge.PackageCode, PackageType.Official, discharge.DischargeDate,
            discharge.DischargeOperator, discharge.DischargedCubicMeters,
            $"Pacco {discharge.PackageCode} scaricato correttamente · MC scaricati: {discharge.DischargedCubicMeters:N6}");
    }

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

    [GeneratedRegex("^[A-Z0-9]{2,8}-[0-9]+-[0-9]{2}-[PS][0-9]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageCodePattern();
}