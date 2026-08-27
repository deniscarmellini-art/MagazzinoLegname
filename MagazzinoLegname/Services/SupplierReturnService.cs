using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class SupplierReturnService
{
    private readonly InventoryProjectionService _inventory = InventoryProjectionService.Shared;
    public static SupplierReturnService Shared { get; } = new();
    private SupplierReturnService() { }

    public IReadOnlyList<InventoryPackage> GetReturnablePackages(Guid loadId) =>
        _inventory.BuildInventory(false).Where(item => item.LoadId == loadId).OrderBy(item => item.PackageNumber).ToArray();

    public SupplierReturnResult ReturnEntireLoad(Guid loadId, string operatorName, string reason,
        string? note = null, string? documentReference = null) =>
        ReturnPackages(loadId, GetReturnablePackages(loadId).Select(item => item.PackageCode), operatorName,
            reason, note, documentReference);

    public SupplierReturnResult ReturnPackages(Guid loadId, IEnumerable<string> packageCodes,
        string operatorName, string reason, string? note = null, string? documentReference = null)
    {
        if (string.IsNullOrWhiteSpace(operatorName)) throw new InvalidOperationException("Indicare l'operatore.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Indicare il motivo del reso.");
        var selectedCodes = packageCodes.Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedCodes.Count == 0) throw new InvalidOperationException("Selezionare almeno un pacco da rendere.");

        var returnable = GetReturnablePackages(loadId);
        if (returnable.Count == 0) throw new InvalidOperationException("Il carico non contiene pacchi restituibili.");
        var selected = returnable.Where(item => selectedCodes.Contains(item.PackageCode)).ToArray();
        if (selected.Length != selectedCodes.Count)
            throw new InvalidOperationException("Uno o più pacchi non appartengono al carico o non sono più presenti.");

        var mode = selected.Length == returnable.Count ? SupplierReturnMode.Total : SupplierReturnMode.Partial;
        return _inventory.RegisterSupplierReturn(selected, mode, operatorName.Trim(), reason.Trim(),
            note?.Trim(), documentReference?.Trim());
    }
}
