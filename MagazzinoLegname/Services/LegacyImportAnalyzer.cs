using System.Text.RegularExpressions;
using System.Globalization;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed partial class LegacyImportAnalyzer
{
    [GeneratedRegex(@"^\s*(?:Pacco\s+)?(?<number>\d+)\s+di\s+(?<total>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PackageRegex();

    public LegacyImportReport Analyze(LegacyWorkbookData workbook)
    {
        foreach (var row in workbook.WarehouseRows) AnalyzeRow(row);
        var supplierGroups = workbook.WarehouseRows.Where(x => !string.IsNullOrWhiteSpace(x.SupplierOriginal)).GroupBy(x => SupplierKey(x.SupplierOriginal!)).ToArray();
        foreach (var group in supplierGroups)
        {
            var canonical = group.Select(x => x.SupplierOriginal!.Trim()).GroupBy(x => x, StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).First().Key;
            foreach (var row in group) row.SupplierNormalized = canonical;
        }
        var proposals = supplierGroups.Select(g => new { Group = g, Variants = g.Select(x => x.SupplierOriginal!).Distinct(StringComparer.Ordinal).OrderBy(x => x).ToArray() })
            .Where(x => x.Variants.Length > 1).Select(x => new SupplierNormalizationProposal(x.Group.First().SupplierNormalized!, string.Join(" → ", x.Variants), x.Group.Count())).ToArray();
        var doubtfulPairs = DetectDoubtfulSupplierPairs(supplierGroups.Select(x => x.First().SupplierNormalized!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        var doubtful = doubtfulPairs.SelectMany(x => new[] { x.Name, x.Similar }).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
        var currentIds = workbook.WarehouseRows.Where(x => !x.IsExcluded && x.Category == LegacyRowCategory.InitialInventory)
            .Select(x => LegacyExcelReader.BuildIdentifier(x.Qr, x.PackageLabel, x.LoadNumber, x.SupplierOriginal, x.Pieces?.ToString(CultureInfo.InvariantCulture), x.InputWidth?.ToString(CultureInfo.InvariantCulture), x.InputThickness?.ToString(CultureInfo.InvariantCulture), x.InputLength?.ToString(CultureInfo.InvariantCulture), x.ExcelRow)).ToArray();
        var currentCounts = currentIds.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var availableCounts = workbook.AvailableIdentifiers.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var matching = currentCounts.Sum(x => Math.Min(x.Value, availableCounts.GetValueOrDefault(x.Key)));
        var issues = workbook.WarehouseRows.SelectMany(x => x.Issues).ToArray();
        var excludedDetails = workbook.WarehouseRows.Where(x => x.IsExcluded).Select(CreateExcludedDetail).OrderBy(x => x.ExcelRow).ToArray();
        var supplierReviews = doubtful.Select(name =>
        {
            var rows = workbook.WarehouseRows.Where(x => string.Equals(x.SupplierNormalized, name, StringComparison.OrdinalIgnoreCase)).ToArray();
            var similar = doubtfulPairs.Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(x => x.Similar)
                .Concat(doubtfulPairs.Where(x => x.Similar.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(x => x.Name)).Distinct(StringComparer.OrdinalIgnoreCase);
            return new LegacySupplierReview(name, rows.Length, rows.Min(x => x.Date), rows.Max(x => x.Date), string.Join(", ", similar));
        }).ToArray();
        var legacyQualityReviews = workbook.WarehouseRows.Where(x => x.QualityNormalized is not null and not ("C" or "VISTA"))
            .GroupBy(x => x.QualityOriginal!.Trim(), StringComparer.Ordinal)
            .Select(x => new LegacyQualityReview(x.Key, x.Count(), x.Sum(r => r.RecalculatedPhysicalCubicMeters ?? 0m), x.Min(r => r.Date), x.Max(r => r.Date)))
            .OrderByDescending(x => x.Rows).ThenBy(x => x.OriginalValue).ToArray();
        return new LegacyImportReport
        {
            FilePath = workbook.FilePath, FoundColumns = workbook.FoundColumns, Rows = workbook.WarehouseRows, Issues = issues,
            SupplierNormalizations = proposals, SuppliersToVerify = doubtful,
            Qualities = workbook.WarehouseRows.Where(x => !string.IsNullOrWhiteSpace(x.QualityNormalized)).GroupBy(x => x.QualityNormalized!).Select(x => new LegacyQualityCount(x.Key, x.Count(), x.Key is not ("C" or "VISTA"))).OrderByDescending(x => x.Rows).ToArray(),
            IssueCounts = issues.GroupBy(x => x.Type).Select(x => new LegacyIssueCount(x.Key, x.Count())).OrderByDescending(x => x.Count).ToArray(),
            ExcludedRowDetails = excludedDetails,
            ExcludedReasonCounts = excludedDetails.GroupBy(x => x.PrimaryReason).Select(x => new LegacyExcludedReasonCount(x.Key, x.Count())).OrderByDescending(x => x.Count).ToArray(),
            SupplierReviews = supplierReviews, LegacyQualityReviews = legacyQualityReviews,
            AvailableSheetRows = workbook.AvailableSheetRows, MatchingAvailableRows = matching, MissingFromAvailableSheet = currentIds.Length - matching, ExtraInAvailableSheet = workbook.AvailableIdentifiers.Count - matching
        };
    }

    private static LegacyExcludedRowDetail CreateExcludedDetail(LegacyStagingRow row)
    {
        var errors = row.Issues.Where(x => x.Severity == LegacyIssueSeverity.Error).ToArray();
        var primary = errors.Select(x => x.Type).OrderBy(ExclusionPriority).FirstOrDefault() switch
        {
            "Pezzi" => "Pezzi <= 0", "Data" => "Data non valida", "Fornitore" => "Fornitore mancante",
            "Carico" => "Carico mancante", "Dimensioni" => "Dimensioni mancanti", _ => "Altro"
        };
        return new(row.ExcelRow, row.SupplierOriginal, row.Date, row.LoadNumber, row.PackageLabel, row.Pieces, row.InputWidth,
            row.InputThickness, row.InputLength, row.QualityOriginal, row.FinishedRawValue,
            string.Join("; ", errors.Select(x => x.Problem).Distinct()), LegacyIssueSeverity.Error, primary);
    }
    private static int ExclusionPriority(string type) => type switch { "Pezzi" => 0, "Data" => 1, "Fornitore" => 2, "Carico" => 3, "Dimensioni" => 4, _ => 5 };

    private static void AnalyzeRow(LegacyStagingRow row)
    {
        row.Category = !string.IsNullOrWhiteSpace(row.FinishedRawValue) ? LegacyRowCategory.ClosedHistory : LegacyRowCategory.InitialInventory;
        if (row.Category == LegacyRowCategory.ClosedHistory && !row.FinishedOn.HasValue)
            Add(row, "Chiusura legacy", $"Chiusura legacy senza data: {row.FinishedRawValue}", LegacyIssueSeverity.Warning, "Conservare il valore originale come informazione storica.");
        row.QualityNormalized = row.QualityOriginal?.Trim() switch { { } q when q.Equals("c", StringComparison.OrdinalIgnoreCase) => "C", { } q when q.Equals("vista", StringComparison.OrdinalIgnoreCase) => "VISTA", { } q => q, _ => null };
        row.IsClassified = ParseClassified(row.ClassifiedOriginal);
        Required(row, row.Pieces is > 0, "Pezzi non validi", "Pezzi", "Verificare e correggere la quantità nel file sorgente.");
        Required(row, row.InputThickness is > 0, "Spessore mancante/non valido", "Dimensioni", "Verificare la colonna B (spessore ingresso).");
        Required(row, row.InputWidth is > 0, "Larghezza mancante/non valida", "Dimensioni", "Verificare la colonna A (larghezza ingresso).");
        Required(row, row.InputLength is > 0, "Lunghezza mancante/non valida", "Dimensioni", "Verificare la colonna C (lunghezza ingresso).");
        Required(row, row.Date.HasValue, "Data mancante/non valida", "Data", "Verificare la data storica.");
        Required(row, !string.IsNullOrWhiteSpace(row.SupplierOriginal), "Fornitore mancante", "Fornitore", "Associare manualmente il fornitore.");
        Required(row, !string.IsNullOrWhiteSpace(row.LoadNumber), "Carico mancante", "Carico", "Preservare il numero originale dopo verifica manuale.");
        if (row.Pieces > 0 && row.InputThickness > 0 && row.InputWidth > 0 && row.InputLength > 0)
        {
            row.RecalculatedPhysicalCubicMeters = row.Pieces.Value * row.InputThickness.Value * row.InputWidth.Value * row.InputLength.Value / 1_000_000_000m;
            if (row.ExcelCubicMeters.HasValue)
            {
                row.CubicMetersDifference = row.RecalculatedPhysicalCubicMeters - row.ExcelCubicMeters;
                if (Math.Abs(row.CubicMetersDifference.Value) > 0.000001m) Add(row, "MC incoerenti", $"Differenza MC {row.CubicMetersDifference:N6}", LegacyIssueSeverity.Warning, "Confrontare formula e valore Excel; non viene applicata alcuna correzione.");
            }
        }
        if (string.IsNullOrWhiteSpace(row.PackageLabel)) Add(row, "Etichetta mancante", "Storico legacy senza identificazione pacco moderna", LegacyIssueSeverity.Warning, "Non inventare il numero pacco.");
        else
        {
            var match = PackageRegex().Match(row.PackageLabel);
            if (match.Success) { row.PackageNumber = int.Parse(match.Groups["number"].Value); row.TotalPackages = int.Parse(match.Groups["total"].Value); }
            else Add(row, "Formato pacco", $"Formato non interpretabile: {row.PackageLabel}", LegacyIssueSeverity.Warning, "Preservare il testo e verificare manualmente.");
        }
        if (row.Category == LegacyRowCategory.InitialInventory && row.IsClassified is null) Add(row, "Classificazione", "Stato classificazione non interpretabile; candidato Da classificare", LegacyIssueSeverity.Warning, "Verificare lo stato prima dell'importazione definitiva.");
    }
    private static void Required(LegacyStagingRow row, bool valid, string problem, string type, string action) { if (!valid) { row.IsExcluded = true; Add(row, type, problem, LegacyIssueSeverity.Error, action); } }
    private static void Add(LegacyStagingRow row, string type, string problem, LegacyIssueSeverity severity, string action) => row.Issues.Add(new(row.ExcelRow, type, problem, severity, action));
    private static bool? ParseClassified(string? value) => value?.Trim().ToLowerInvariant() switch { "si" or "sì" or "yes" or "x" or "vero" or "true" or "1" or "classificato" => true, "no" or "falso" or "false" or "0" or "" or null => false, _ => null };
    private static string SupplierKey(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    private static (string Name, string Similar)[] DetectDoubtfulSupplierPairs(string[] names)
    {
        var result = new List<(string, string)>();
        for (var i = 0; i < names.Length; i++) for (var j = i + 1; j < names.Length; j++)
            if (!names[i].Equals(names[j], StringComparison.OrdinalIgnoreCase) && Levenshtein(names[i].ToUpperInvariant(), names[j].ToUpperInvariant()) == 1) result.Add((names[i], names[j]));
        return result.ToArray();
    }
    private static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1]; for (var i = 0; i <= a.Length; i++) d[i, 0] = i; for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++) for (var j = 1; j <= b.Length; j++) d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
}
