using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Services;

// Read-through SQL snapshot, refreshed by the consultation boundary. Never an import destination.
public sealed class LegacyHistoricalStore
{
    public static LegacyHistoricalStore Shared { get; } = new();
    private IReadOnlyList<LegacyHistoricalRecord> _records = [];
    private LegacyHistoricalStore() { }
    public event EventHandler? HistoryChanged;
    public IReadOnlyList<LegacyHistoricalRecord> Records => _records;
    public void Reload()
    {
        using var db = SqlPersistenceRoot.ContextFactory.CreateDbContext();
        var rows = db.LegacyHistoricalRecords.AsNoTracking().Include(x => x.Batch).OrderBy(x => x.ArrivalDate).ThenBy(x => x.ExcelRow).ToArray();
        _records = rows.Select(x => new LegacyHistoricalRecord {
            Id = x.Id, HistoricalLoadId = x.HistoricalLoadId, BatchId = x.BatchId, FileFingerprint = x.Batch.FileFingerprint,
            ImportKey = x.ImportKey, ExcelRow = x.ExcelRow, SupplierName = x.SupplierNameSnapshot, ArrivalDate = x.ArrivalDate,
            LoadNumber = x.LoadNumber, PackageLabel = x.PackageLabel, Pieces = x.Pieces,
            IncomingThickness = x.IncomingThickness, IncomingWidth = x.IncomingWidth, IncomingLength = x.IncomingLength,
            QualityOriginal = x.QualityOriginal, QualityNormalized = x.QualityNormalized, Certification = x.Certification,
            PhysicalCubicMeters = x.PhysicalCubicMeters, ExcelCubicMeters = x.LegacyAvailableCubicMeters,
            LegacyEstimatedCubicMeters = x.LegacyEstimatedCubicMeters, IsClassified = x.IsClassified, ClassificationDate = x.ClassificationDate,
            FinishedRawValue = x.FinishedRawValue ?? "", FinishedOn = x.FinishedOn, LegacyQr = x.LegacyQr
        }).ToArray();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
#if DEBUG
    public void ResetTestImportRegistry() { _records = []; HistoryChanged?.Invoke(this, EventArgs.Empty); }
#endif
}
