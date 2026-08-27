using System.IO;
using System.Security.Cryptography;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class LegacyHistoricalStore
{
    private readonly object _sync = new();
    private readonly List<LegacyHistoricalRecord> _records = [];
    private readonly List<LegacyClosedHistoryImportBatch> _batches = [];
    private readonly HashSet<string> _importedKeys = new(StringComparer.OrdinalIgnoreCase);
    public static LegacyHistoricalStore Shared { get; } = new();
    private LegacyHistoricalStore() { }

    public event EventHandler? HistoryChanged;
    public IReadOnlyList<LegacyHistoricalRecord> Records { get { lock (_sync) return _records.ToArray(); } }
    public IReadOnlyList<LegacyClosedHistoryImportBatch> Batches { get { lock (_sync) return _batches.ToArray(); } }

    public LegacyClosedHistoryImportPlan BuildPlan(LegacyImportReport report)
    {
        var rows = report.Rows.Where(x => !x.IsExcluded && x.Category == LegacyRowCategory.ClosedHistory).ToArray();
        var fingerprint = Fingerprint(report.FilePath);
        var collisions = new List<string>();
        lock (_sync)
        {
            if (_batches.Any(x => x.FileFingerprint == fingerprint))
                collisions.Add("Lo storico chiuso di questo file è già stato importato.");
            foreach (var row in rows.Where(x => _importedKeys.Contains(ImportKey(fingerprint, x))))
                collisions.Add($"Riga storica Excel {row.ExcelRow} già importata.");
        }
        return new LegacyClosedHistoryImportPlan { BatchId = Guid.NewGuid(), FilePath = report.FilePath,
            FileFingerprint = fingerprint, Rows = rows, Collisions = collisions };
    }

    public LegacyClosedHistoryImportResult Commit(LegacyClosedHistoryImportPlan plan)
    {
        lock (_sync)
        {
            if (!plan.CanCommit) throw new InvalidOperationException(string.Join(Environment.NewLine, plan.Collisions));
            if (_batches.Any(x => x.FileFingerprint == plan.FileFingerprint)
                || plan.Rows.Any(x => _importedKeys.Contains(ImportKey(plan.FileFingerprint, x))))
                throw new InvalidOperationException("Importazione storico bloccata: file o righe già registrati.");

            var loadIds = plan.Rows.GroupBy(LoadKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, _ => Guid.NewGuid(), StringComparer.OrdinalIgnoreCase);
            var records = plan.Rows.Select(row => CreateRecord(plan, row, loadIds[LoadKey(row)])).ToArray();
            _records.AddRange(records);
            foreach (var record in records) _importedKeys.Add(record.ImportKey);
            var batch = new LegacyClosedHistoryImportBatch(plan.BatchId, Path.GetFileName(plan.FilePath),
                plan.FileFingerprint, DateTime.Now, records.Length, plan.DistinctLoads, plan.PhysicalCubicMeters);
            _batches.Add(batch);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return new(batch, records.Length, plan.DistinctLoads, plan.PhysicalCubicMeters);
        }
    }

    public void ResetTestImportRegistry()
    {
        lock (_sync) { _records.Clear(); _batches.Clear(); _importedKeys.Clear(); }
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public static string ImportKey(string fingerprint, LegacyStagingRow row) =>
        $"{fingerprint}|{row.ExcelRow}|{row.LoadNumber}|{row.PackageLabel}";

    private static string LoadKey(LegacyStagingRow row) => $"{row.SupplierNormalized ?? row.SupplierOriginal}|{row.LoadNumber}";

    private static LegacyHistoricalRecord CreateRecord(LegacyClosedHistoryImportPlan plan, LegacyStagingRow row, Guid historicalLoadId) => new()
    {
        HistoricalLoadId = historicalLoadId, BatchId = plan.BatchId, FileFingerprint = plan.FileFingerprint, ImportKey = ImportKey(plan.FileFingerprint, row),
        ExcelRow = row.ExcelRow, SupplierName = row.SupplierNormalized ?? row.SupplierOriginal!, ArrivalDate = row.Date!.Value.Date,
        LoadNumber = row.LoadNumber!, PackageLabel = row.PackageLabel, PackageNumber = row.PackageNumber,
        TotalPackages = row.TotalPackages, Pieces = row.Pieces!.Value, IncomingThickness = row.InputThickness!.Value,
        IncomingWidth = row.InputWidth!.Value, IncomingLength = row.InputLength!.Value,
        QualityOriginal = row.QualityOriginal, QualityNormalized = row.QualityNormalized, Certification = row.Certification,
        PhysicalCubicMeters = row.RecalculatedPhysicalCubicMeters ?? 0m, ExcelCubicMeters = row.ExcelCubicMeters,
        LegacyEstimatedCubicMeters = row.LegacyEstimatedCubicMeters, IsClassified = row.IsClassified,
        ClassificationDate = row.ClassificationDate, FinishedRawValue = row.FinishedRawValue!, FinishedOn = row.FinishedOn,
        LegacyQr = row.Qr
    };

    private static string Fingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
