using System.Text.RegularExpressions;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed partial class LoadNumberSequenceService
{
    private static readonly Lazy<LoadNumberSequenceService> SharedInstance = new(() => new());
    private readonly object _assignmentLock = new();
    private readonly HashSet<(Guid SupplierId, int Year, int AnnualSequence)> _reservedAssignments = [];

    private LoadNumberSequenceService() { }
    public static LoadNumberSequenceService Shared => SharedInstance.Value;

    public LoadNumberAssignment PreviewNext(Guid supplierId, int year)
    {
        lock (_assignmentLock) return new(supplierId, year, FindNextProgressive(supplierId, year));
    }
    public LoadNumberAssignment ReserveNext(Guid supplierId, int year)
    {
        lock (_assignmentLock)
        {
            var next = FindNextProgressive(supplierId, year);
            _reservedAssignments.Add((supplierId, year, next));
            return new(supplierId, year, next);
        }
    }
    public bool IsAlreadyUsed(Guid supplierId, int year, int annualSequence)
    {
        lock (_assignmentLock) return ExistingAssignments(supplierId, year).Contains(annualSequence)
            || _reservedAssignments.Contains((supplierId, year, annualSequence));
    }
    private int FindNextProgressive(Guid supplierId, int year) => ExistingAssignments(supplierId, year)
        .Concat(_reservedAssignments.Where(x => x.SupplierId == supplierId && x.Year == year).Select(x => x.AnnualSequence))
        .DefaultIfEmpty(0).Max() + 1;

    private IEnumerable<int> ExistingAssignments(Guid supplierId, int year)
    {
        var suppliers = SupplierCatalogService.Shared.Suppliers;
        foreach (var load in ClassificationWorkflowService.Shared.Loads)
        {
            var linkedSupplierId = load.SupplierId != Guid.Empty ? load.SupplierId
                : suppliers.FirstOrDefault(x => x.Code.Equals(load.SupplierCode, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty;
            if (linkedSupplierId != supplierId) continue;
            if (load.LoadYear == year && load.AnnualProgressive is > 0) { yield return load.AnnualProgressive.Value; continue; }
            if (!string.IsNullOrWhiteSpace(load.LegacyLoadNumber)
                && TryParseLegacyLoadNumber(load.LegacyLoadNumber, load.ArrivalDate.Year, out var legacyYear, out var legacyProgressive)
                && legacyYear == year) { yield return legacyProgressive; continue; }
            if (TryParseCurrentLoadNumber(load.LoadNumber, load.ArrivalDate.Year, out var currentYear, out var currentProgressive)
                && currentYear == year) yield return currentProgressive;
        }
        var supplierName = suppliers.FirstOrDefault(x => x.Id == supplierId)?.Name;
        if (string.IsNullOrWhiteSpace(supplierName)) yield break;
        foreach (var record in LegacyHistoricalStore.Shared.Records
            .Where(item => item.SupplierName.Equals(supplierName, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.LoadNumber, StringComparer.OrdinalIgnoreCase).Select(group => group.First()))
            if (TryParseLegacyLoadNumber(record.LoadNumber, record.ArrivalDate.Year, out var legacyYear, out var legacyProgressive)
                && legacyYear == year) yield return legacyProgressive;
    }

    public static bool TryParseLegacyLoadNumber(string value, int referenceYear, out int year, out int progressive)
    {
        year = 0; progressive = 0;
        var match = LegacyLoadPattern().Match(value ?? string.Empty);
        if (!match.Success || !int.TryParse(match.Groups["year"].Value, out var shortYear)
            || !int.TryParse(match.Groups["progressive"].Value, out progressive) || progressive <= 0) return false;
        year = referenceYear / 100 * 100 + shortYear;
        if (year - referenceYear > 50) year -= 100; else if (referenceYear - year > 50) year += 100;
        return true;
    }
    private static bool TryParseCurrentLoadNumber(string value, int referenceYear, out int year, out int progressive)
    {
        year = 0; progressive = 0;
        var match = CurrentLoadPattern().Match(value ?? string.Empty);
        if (!match.Success || !int.TryParse(match.Groups["progressive"].Value, out progressive)
            || !int.TryParse(match.Groups["year"].Value, out var shortYear) || progressive <= 0) return false;
        year = referenceYear / 100 * 100 + shortYear;
        return true;
    }
    [GeneratedRegex(@"^\s*.+?\s+(?<year>\d{2})\s*-\s*(?<progressive>\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex LegacyLoadPattern();
    [GeneratedRegex(@"^\s*(?<progressive>\d+)\s*-\s*(?<year>\d{2})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrentLoadPattern();
}
