using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class LoadNumberSequenceService
{
    private static readonly Lazy<LoadNumberSequenceService> SharedInstance = new(() => new());
    private readonly object _assignmentLock = new();
    private readonly HashSet<(Guid SupplierId, int Year, int AnnualSequence)> _usedAssignments = [];

    private LoadNumberSequenceService()
    {
        SeedFromSharedLoadHistory();
    }

    public static LoadNumberSequenceService Shared => SharedInstance.Value;

    public LoadNumberAssignment PreviewNext(Guid supplierId, int year)
    {
        lock (_assignmentLock)
            return new(supplierId, year, FindNextAvailable(supplierId, year));
    }

    // Il controllo e la prenotazione avvengono sotto lo stesso lock: anche se la preview
    // fosse diventata obsoleta, viene ricalcolato il primo progressivo libero. Nel database
    // futuro questa operazione dovrà essere transazionale e protetta da UNIQUE
    // (SupplierId, Year, AnnualSequence).
    public LoadNumberAssignment ReserveNext(Guid supplierId, int year)
    {
        lock (_assignmentLock)
        {
            var next = FindNextAvailable(supplierId, year);
            while (!_usedAssignments.Add((supplierId, year, next))) next++;
            return new(supplierId, year, next);
        }
    }

    public bool IsAlreadyUsed(Guid supplierId, int year, int annualSequence)
    {
        lock (_assignmentLock)
            return _usedAssignments.Contains((supplierId, year, annualSequence));
    }

    private int FindNextAvailable(Guid supplierId, int year)
    {
        var max = _usedAssignments
            .Where(item => item.SupplierId == supplierId && item.Year == year)
            .Select(item => item.AnnualSequence)
            .DefaultIfEmpty(0).Max();
        var next = max + 1;
        while (_usedAssignments.Contains((supplierId, year, next))) next++;
        return next;
    }

    private void SeedFromSharedLoadHistory()
    {
        var suppliers = SupplierCatalogService.Shared.Suppliers;
        foreach (var load in ClassificationWorkflowService.Shared.Loads)
        {
            var supplier = suppliers.FirstOrDefault(item =>
                item.Code.Equals(load.SupplierCode, StringComparison.OrdinalIgnoreCase));
            if (supplier is null || !TryParseLoadNumber(load.LoadNumber, out var sequence, out var shortYear)) continue;
            var century = load.ArrivalDate.Year / 100 * 100;
            _usedAssignments.Add((supplier.Id, century + shortYear, sequence));
        }

        // Carichi demo già conclusi/archiviati: restano nello storico numerazione anche se
        // non compaiono più tra i carichi operativi o tra i pacchi presenti in giacenza.
        SeedArchived("SEG", 2026, 1, 2);
        SeedArchived("LEG", 2026, 1);
    }

    private void SeedArchived(string supplierCode, int year, params int[] sequences)
    {
        var supplier = SupplierCatalogService.Shared.Suppliers.First(item =>
            item.Code.Equals(supplierCode, StringComparison.OrdinalIgnoreCase));
        foreach (var sequence in sequences) _usedAssignments.Add((supplier.Id, year, sequence));
    }

    private static bool TryParseLoadNumber(string value, out int sequence, out int shortYear)
    {
        sequence = 0; shortYear = 0;
        var parts = value.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out sequence)
            && int.TryParse(parts[1], out shortYear) && sequence > 0 && shortYear is >= 0 and <= 99;
    }
}
