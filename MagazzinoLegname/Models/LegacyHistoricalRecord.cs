using System.Text.RegularExpressions;

namespace MagazzinoLegname.Models;

public sealed record LegacyHistoricalRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid HistoricalLoadId { get; init; }
    public required Guid BatchId { get; init; }
    public required string FileFingerprint { get; init; }
    public required string ImportKey { get; init; }
    public required int ExcelRow { get; init; }
    public required string SupplierName { get; init; }
    public required DateTime ArrivalDate { get; init; }
    public required string LoadNumber { get; init; }
    public string? PackageLabel { get; init; }
    public int? PackageNumber { get; init; }
    public int? TotalPackages { get; init; }
    public required decimal Pieces { get; init; }
    public required decimal IncomingThickness { get; init; }
    public required decimal IncomingWidth { get; init; }
    public required decimal IncomingLength { get; init; }
    public string? QualityOriginal { get; init; }
    public string? QualityNormalized { get; init; }
    public string? Certification { get; init; }
    public required decimal PhysicalCubicMeters { get; init; }
    public decimal? ExcelCubicMeters { get; init; }
    public decimal? LegacyEstimatedCubicMeters { get; init; }
    public decimal LegacyAvailableCubicMeters => LegacyEstimatedCubicMeters
        ?? ExcelCubicMeters
        ?? PhysicalCubicMeters;
    public bool? IsClassified { get; init; }
    public DateTime? ClassificationDate { get; init; }
    public required string FinishedRawValue { get; init; }
    public DateTime? FinishedOn { get; init; }
    public string? LegacyClosureText => FinishedOn.HasValue ? null : FinishedRawValue;
    public bool IsSupplierReturn => LegacyMovementClassifier.IsSupplierReturn(FinishedRawValue);
    public string MovementType => IsSupplierReturn ? "Reso" : "Scarico";
    public DateTime? ReturnedDate => IsSupplierReturn ? FinishedOn : null;
    public decimal? ReturnedPhysicalCubicMeters => IsSupplierReturn ? PhysicalCubicMeters : null;
    public string FinalStatus => IsSupplierReturn ? "Reso" : "Scaricato";
    public string? LegacyQr { get; init; }
}

public static class LegacyMovementClassifier
{
    private static readonly Regex SupplierReturnPattern = new(
        @"(^|[^\p{L}])reso([^\p{L}]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsSupplierReturn(string? finishedRawValue) =>
        !string.IsNullOrWhiteSpace(finishedRawValue)
        && SupplierReturnPattern.IsMatch(finishedRawValue.Trim());
}

public sealed record LegacyClosedHistoryImportBatch(Guid Id, string FileName, string FileFingerprint,
    DateTime ImportedAt, int ImportedRecords, int DistinctLoads, decimal PhysicalCubicMeters);

public sealed class LegacyClosedHistoryImportPlan
{
    public required Guid BatchId { get; init; }
    public required string FilePath { get; init; }
    public required string FileFingerprint { get; init; }
    public required IReadOnlyList<LegacyStagingRow> Rows { get; init; }
    public required IReadOnlyList<string> Collisions { get; init; }
    public int RecordCount => Rows.Count;
    public int DistinctLoads => Rows.Select(x => $"{x.SupplierNormalized ?? x.SupplierOriginal}|{x.LoadNumber}")
        .Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public int DistinctSuppliers => Rows.Select(x => x.SupplierNormalized ?? x.SupplierOriginal)
        .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    public decimal PhysicalCubicMeters => Rows.Sum(x => x.RecalculatedPhysicalCubicMeters ?? 0m);
    public DateTime? FirstDate => Rows.Min(x => x.Date);
    public DateTime? LastDate => Rows.Max(x => x.Date);
    public int RowsWithWarnings => Rows.Count(x => x.Issues.Any(issue => issue.Severity == LegacyIssueSeverity.Warning));
    public bool CanCommit => Collisions.Count == 0;
    public string CoveredPeriod => FirstDate.HasValue && LastDate.HasValue
        ? $"{FirstDate:dd/MM/yyyy} – {LastDate:dd/MM/yyyy}" : "N/D";
}

public sealed record LegacyClosedHistoryImportResult(LegacyClosedHistoryImportBatch Batch, int ImportedRecords,
    int DistinctLoads, decimal PhysicalCubicMeters);
