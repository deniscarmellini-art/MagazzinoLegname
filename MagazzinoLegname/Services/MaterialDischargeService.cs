using System.Text.RegularExpressions;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Repositories;

namespace MagazzinoLegname.Services;

public sealed partial class MaterialDischargeService
{
    private readonly InventoryProjectionService _inventory = InventoryProjectionService.Shared;

    public PackageLookupResult Lookup(string qrPayload)
    {
        if (!TryExtractPackageCode(qrPayload, out var packageCode))
            return new(PackageLookupStatus.InvalidQr, "QR non valido");

        SqlPackageTerminalState sqlState;
        try { sqlState = SqlPersistenceRoot.PackageTerminals.FindState(packageCode); }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
        if (!sqlState.Exists) return new(PackageLookupStatus.NotFound, "Pacco non trovato");
        if (sqlState.TerminalState is { } terminalState) return TerminalStateResult(packageCode, terminalState);

        try
        {
            ClassificationWorkflowService.Shared.ReloadInboundLoads();
            _inventory.ReloadSqlTerminalMovements();
        }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }

        var package = _inventory.FindPackage(packageCode);
        if (package is null) return new(PackageLookupStatus.NotFound, "Pacco non trovato");
        if (package.IsSupplementary)
        {
            var previousExit = _inventory.FindSupplementaryExit(packageCode);
            if (previousExit is not null || !package.IsPresent)
                return TerminalStateResult(packageCode, PackageTerminalState.SupplementaryExited);
            return new(PackageLookupStatus.Ready,
                "Pacco supplementare riconosciuto. Verrà registrata l'uscita fisica senza movimento di MC.", package);
        }

        var previousMovement = _inventory.FindMovement(packageCode);
        if (previousMovement is not null)
            return TerminalStateResult(packageCode, PackageTerminalState.Discharged);
        if (!package.IsPresent)
            return package.PackageStatus switch
            {
                "Reso" => TerminalStateResult(packageCode, PackageTerminalState.Returned),
                "Rimosso manualmente" => TerminalStateResult(packageCode, PackageTerminalState.ManuallyRemoved),
                _ => TerminalStateResult(packageCode, PackageTerminalState.Discharged)
            };
        if (package.ClassificationStatus != "Classificato")
            return new(PackageLookupStatus.NotClassified,
                "Scarico non consentito. Il materiale deve essere classificato prima dello scarico.", package);
        if (!package.UsesRealCubicMeters)
            return new(PackageLookupStatus.WasteAdjustmentRequired,
                "Scarico non consentito. È necessario completare la rettifica scarti.", package);
        return new(PackageLookupStatus.Ready, "Pacco pronto per lo scarico", package);
    }

    private static PackageLookupResult TerminalStateResult(string packageCode, PackageTerminalState state) => state switch
    {
        PackageTerminalState.Returned => new(PackageLookupStatus.Returned,
            $"Pacco reso. Il pacco {packageCode} risulta già reso al fornitore."),
        PackageTerminalState.ManuallyRemoved => new(PackageLookupStatus.ManuallyRemoved,
            $"Pacco rimosso. Il pacco {packageCode} risulta rimosso dalla giacenza."),
        PackageTerminalState.SupplementaryExited => new(PackageLookupStatus.SupplementaryAlreadyExited,
            $"Pacco supplementare già uscito. Il pacco {packageCode} risulta già uscito dalla giacenza fisica."),
        _ => new(PackageLookupStatus.AlreadyDischarged,
            $"Pacco già scaricato. Il pacco {packageCode} risulta già scaricato.")
    };

    public PackageExitResult Confirm(InventoryPackage package, string operatorName)
    {
        try
        {
            var result = SqlPersistenceRoot.PackageTerminals.Discharge(package.PackageCode, operatorName);
            ClassificationWorkflowService.Shared.ReloadInboundLoads();
            _inventory.ReloadSqlTerminalMovements();
            return result;
        }
        catch (PackageAlreadyExitedException) { throw; }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
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
