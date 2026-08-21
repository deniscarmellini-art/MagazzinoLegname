using System.Collections.ObjectModel;

namespace MagazzinoLegname.Models;

public enum LegacyRowCategory { ClosedHistory, InitialInventory }
public enum LegacyIssueSeverity { Warning, Error }

public sealed record LegacyImportIssue(int ExcelRow, string Type, string Problem, LegacyIssueSeverity Severity, string ProposedAction);

public sealed class LegacyStagingRow
{
    public int ExcelRow { get; init; }
    public string? SupplierOriginal { get; init; }
    public string? SupplierNormalized { get; set; }
    public DateTime? Date { get; init; }
    public string? LoadNumber { get; init; }
    public string? PackageLabel { get; init; }
    public int? PackageNumber { get; set; }
    public int? TotalPackages { get; set; }
    public decimal? Pieces { get; init; }
    public decimal? InputWidth { get; init; }
    public decimal? InputThickness { get; init; }
    public decimal? InputLength { get; init; }
    public DateTime? FinishedOn { get; init; }
    public string? FinishedRawValue { get; init; }
    public string? QualityOriginal { get; init; }
    public string? QualityNormalized { get; set; }
    public decimal? RealWidth { get; init; }
    public string? Certification { get; init; }
    public decimal? LinearMeters { get; init; }
    public decimal? ExcelCubicMeters { get; init; }
    public decimal? LegacyEstimatedCubicMeters { get; init; }
    public string? ClassifiedOriginal { get; init; }
    public DateTime? ClassificationDate { get; init; }
    public string? Qr { get; init; }
    public string? Year { get; init; }
    public decimal? RecalculatedPhysicalCubicMeters { get; set; }
    public decimal? CubicMetersDifference { get; set; }
    public bool? IsClassified { get; set; }
    public LegacyRowCategory Category { get; set; }
    public bool IsExcluded { get; set; }
    public ObservableCollection<LegacyImportIssue> Issues { get; } = [];
}

public sealed record SupplierNormalizationProposal(string CanonicalName, string Variants, int Rows);
public sealed record LegacyQualityCount(string Quality, int Rows, bool IsLegacy);
public sealed record LegacyIssueCount(string Type, int Count);
public sealed record LegacyExcludedRowDetail(
    int ExcelRow, string? Supplier, DateTime? Date, string? LoadNumber, string? PackageLabel,
    decimal? Pieces, decimal? InputWidth, decimal? InputThickness, decimal? InputLength,
    string? Quality, string? FinishedRawValue, string ExclusionReason, LegacyIssueSeverity Severity,
    string PrimaryReason);
public sealed record LegacyExcludedReasonCount(string Reason, int Count);
public sealed record LegacySupplierReview(string OriginalName, int Rows, DateTime? FirstDate, DateTime? LastDate, string SimilarName);
public sealed record LegacyQualityReview(string OriginalValue, int Rows, decimal PhysicalCubicMeters, DateTime? FirstDate, DateTime? LastDate);

public sealed class LegacyWorkbookData
{
    public required string FilePath { get; init; }
    public required IReadOnlyList<string> FoundColumns { get; init; }
    public required IReadOnlyList<LegacyStagingRow> WarehouseRows { get; init; }
    public required IReadOnlyList<string> AvailableIdentifiers { get; init; }
    public int AvailableSheetRows { get; init; }
}

public sealed class LegacyImportReport
{
    public required string FilePath { get; init; }
    public required IReadOnlyList<string> FoundColumns { get; init; }
    public required IReadOnlyList<LegacyStagingRow> Rows { get; init; }
    public required IReadOnlyList<LegacyImportIssue> Issues { get; init; }
    public required IReadOnlyList<SupplierNormalizationProposal> SupplierNormalizations { get; init; }
    public required IReadOnlyList<string> SuppliersToVerify { get; init; }
    public required IReadOnlyList<LegacyQualityCount> Qualities { get; init; }
    public required IReadOnlyList<LegacyIssueCount> IssueCounts { get; init; }
    public required IReadOnlyList<LegacyExcludedRowDetail> ExcludedRowDetails { get; init; }
    public required IReadOnlyList<LegacyExcludedReasonCount> ExcludedReasonCounts { get; init; }
    public required IReadOnlyList<LegacySupplierReview> SupplierReviews { get; init; }
    public required IReadOnlyList<LegacyQualityReview> LegacyQualityReviews { get; init; }
    public int TotalRows => Rows.Count;
    public int ValidRows => Rows.Count(x => !x.IsExcluded);
    public int RowsWithIssues => Rows.Count(x => x.Issues.Count > 0);
    public int ExcludedRows => Rows.Count(x => x.IsExcluded);
    public int ClosedHistoryRows => Rows.Count(x => !x.IsExcluded && x.Category == LegacyRowCategory.ClosedHistory);
    public int CurrentInventoryRows => Rows.Count(x => !x.IsExcluded && x.Category == LegacyRowCategory.InitialInventory);
    public int HistoricalLoads => Rows.Where(x => !x.IsExcluded && x.Category == LegacyRowCategory.ClosedHistory).Select(x => x.LoadNumber).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public int CurrentLoads => Rows.Where(x => !x.IsExcluded && x.Category == LegacyRowCategory.InitialInventory).Select(x => x.LoadNumber).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public int Classified => Rows.Count(x => !x.IsExcluded && x.Category == LegacyRowCategory.InitialInventory && x.IsClassified == true);
    public int ToClassify => Rows.Count(x => !x.IsExcluded && x.Category == LegacyRowCategory.InitialInventory && x.IsClassified != true);
    public decimal HistoricalPhysicalCubicMeters => Rows.Where(x => !x.IsExcluded && x.Category == LegacyRowCategory.ClosedHistory).Sum(x => x.RecalculatedPhysicalCubicMeters ?? 0m);
    public decimal CurrentPhysicalCubicMeters => Rows.Where(x => !x.IsExcluded && x.Category == LegacyRowCategory.InitialInventory).Sum(x => x.RecalculatedPhysicalCubicMeters ?? 0m);
    public decimal ExcelCubicMeters => Rows.Where(x => !x.IsExcluded).Sum(x => x.ExcelCubicMeters ?? 0m);
    public decimal CubicMetersDifference => Rows.Where(x => !x.IsExcluded).Sum(x => x.CubicMetersDifference ?? 0m);
    public decimal CurrentLegacyEstimatedCubicMeters => Rows.Where(x => !x.IsExcluded && x.Category == LegacyRowCategory.InitialInventory).Sum(x => x.LegacyEstimatedCubicMeters ?? 0m);
    public int DistinctSuppliers => Rows.Select(x => x.SupplierOriginal?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public int AvailableSheetRows { get; init; }
    public int MatchingAvailableRows { get; init; }
    public int MissingFromAvailableSheet { get; init; }
    public int ExtraInAvailableSheet { get; init; }
    public int LegacyTextClosureCount => Rows.Count(x => x.Category == LegacyRowCategory.ClosedHistory && !string.IsNullOrWhiteSpace(x.FinishedRawValue) && !x.FinishedOn.HasValue);
    public string ColumnsDisplay => string.Join(" · ", FoundColumns);
    public string ReadOnlyConfirmation => "SIMULAZIONE READ ONLY — nessun carico, pacco, movimento, giacenza, storico o fornitore è stato modificato.";
}
