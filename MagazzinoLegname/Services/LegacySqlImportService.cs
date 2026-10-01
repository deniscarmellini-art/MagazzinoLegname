using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Services;

public sealed record LegacySqlPreview(string FilePath, long FileSize, DateTime LastWriteUtc,
    string Fingerprint, DateTime AnalyzedAtUtc, LegacyImportReport Report,
    IReadOnlyList<string> BlockingErrors, IReadOnlyList<string> Warnings, int KnownRows, int DuplicateRows)
{
    public bool CanImport => BlockingErrors.Count == 0;
    public string FileIdentity => $"{Path.GetFileName(FilePath)}\n{FilePath}\n{FileSize:N0} byte · ultima modifica {LastWriteUtc.ToLocalTime():dd/MM/yyyy HH:mm:ss}\nSHA-256: {Fingerprint}\nAnalisi: {AnalyzedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm:ss}";
    public string Summary => $"Righe lette: {Report.TotalRows} · valide: {Report.ValidRows} · escluse: {Report.ExcludedRows}\n" +
        $"Giacenza: {Report.CurrentLoads} carichi · {Report.CurrentInventoryRows} pacchi · classificati {Report.Classified} · da classificare {Report.ToClassify}\n" +
        $"MC fisici: {Report.CurrentPhysicalCubicMeters:N9} · MC reali ricostruibili: 0 · MC da consolidare: {Report.CurrentPhysicalCubicMeters:N9}\n" +
        $"Valore storico importabile: {Report.CurrentImportableValue:N4} · con prezzo: {Report.CurrentPackagesWithPrice} · senza prezzo: {Report.CurrentPackagesWithoutPrice}\n" +
        $"Storico: {Report.ClosedHistoryRows} · scarichi datati: {Report.Rows.Count(x => x.Category == LegacyRowCategory.ClosedHistory && x.FinishedOn.HasValue && !LegacyMovementClassifier.IsSupplierReturn(x.FinishedRawValue))} · resi: {Report.Rows.Count(x => LegacyMovementClassifier.IsSupplierReturn(x.FinishedRawValue))}\n" +
        $"Altre chiusure legacy: {Report.Rows.Count(x => x.Category == LegacyRowCategory.ClosedHistory && !x.FinishedOn.HasValue && !LegacyMovementClassifier.IsSupplierReturn(x.FinishedRawValue))}\n" +
        $"Anomalie: {Report.RowsWithIssues} · duplicati nel file: {DuplicateRows} · righe già note SQL: {KnownRows}\nBloccanti: {BlockingErrors.Count} · warning: {Warnings.Count}";
}

public sealed record LegacySqlImportResult(Guid? BatchId, int ImportedPackages, int ImportedHistory,
    int SkippedRows, decimal PhysicalCubicMeters, decimal HistoricalValue)
{
    public string Summary => $"Import SQL completato. Batch: {BatchId?.ToString() ?? "già importato"}\nPacchi: {ImportedPackages} · storico: {ImportedHistory} · già noti: {SkippedRows}\nQuadratura SQL verificata: MC {PhysicalCubicMeters:N9} · valore {HistoricalValue:N4}";
}

// One-time migration boundary. No operational workflow methods or RAM commits are used.
public sealed class LegacySqlImportService(IDbContextFactory<MagazzinoDbContext> factory)
{
    public static bool IsEnabled => !string.Equals(Environment.GetEnvironmentVariable("MAGAZZINOLEGNAME_DISABLE_INITIAL_IMPORT"), "1", StringComparison.Ordinal);
    public LegacySqlPreview Analyze(string path)
    {
        using var source = OpenSource(path);
        return AnalyzeLocked(path, source);
    }
    private static FileStream OpenSource(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    private LegacySqlPreview AnalyzeLocked(string path, FileStream source)
    {
        source.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(source));
        var report = new LegacyImportAnalyzer().Analyze(new LegacyExcelReader().Read(path));
        using var db = factory.CreateDbContext();
        var errors = report.Issues.Where(x => x.Severity == LegacyIssueSeverity.Error).Select(x => $"Riga {x.ExcelRow}: {x.Problem}").ToList();
        var warnings = report.Issues.Where(x => x.Severity == LegacyIssueSeverity.Warning).Select(x => $"Riga {x.ExcelRow}: {x.Problem}").ToList();
        var families = db.ThicknessFamilies.AsNoTracking().ToArray();
        foreach (var row in report.Rows)
        {
            void Check(bool valid, string message) { if (!valid) errors.Add($"Riga {row.ExcelRow}: {message}"); }
            Check(!string.IsNullOrWhiteSpace(row.PackageLabel) || !string.IsNullOrWhiteSpace(row.Qr), "identificativo pacco insufficiente (etichetta o QR obbligatorio).");
            Check(row.Pieces is > 0 and <= int.MaxValue && row.Pieces == decimal.Truncate(row.Pieces.Value), "pezzi interi positivi obbligatori.");
            Check(row.Date is { Year: >= 1900 } && row.Date.Value.Date <= DateTime.Today, "data ingresso incoerente.");
            Check(!row.FinishedOn.HasValue || row.FinishedOn >= row.Date && row.FinishedOn.Value.Date <= DateTime.Today, "data uscita incoerente.");
            Check(!row.ClassificationDate.HasValue || row.IsClassified == true && row.ClassificationDate >= row.Date && row.ClassificationDate.Value.Date <= DateTime.Today && (!row.FinishedOn.HasValue || row.ClassificationDate <= row.FinishedOn), "data classificazione incoerente.");
            Check(row.QualityNormalized is "C" or "VISTA", "qualità non riconosciuta.");
            Check(row.IsClassified.HasValue, "stato classificazione ambiguo.");
            Check((row.SupplierNormalized?.Length ?? 0) <= 200 && (row.LoadNumber?.Length ?? 0) <= 80 && (row.PackageLabel?.Length ?? 0) <= 120 && (row.Certification?.Length ?? 0) <= 40 && (row.Qr?.Length ?? 0) <= 1000, "testo oltre i limiti SQL.");
            foreach (var dimension in new[] { row.InputThickness, row.InputWidth, row.InputLength })
                Check(dimension is > 0 and < 100000000m && decimal.Round(dimension.Value,4) == dimension, "dimensione impossibile/non rappresentabile in SQL.");
            if (row.Category == LegacyRowCategory.InitialInventory)
            {
                Check(row.AppliedPrice is > 0 and < 1000000000000000m && decimal.Round(row.AppliedPrice.Value,4) == row.AppliedPrice, "prezzo storico obbligatorio, positivo e rappresentabile con 4 decimali.");
                Check(families.Count(x => row.InputThickness >= x.MinimumIncomingThickness && row.InputThickness <= x.MaximumIncomingThickness) == 1, "spessore senza famiglia univoca.");
                if (row.IsClassified == true) warnings.Add($"Riga {row.ExcelRow}: stato classificato conservato; operatore storico e rettifica non ricostruibili. MC da consolidare, nessun movimento artificiale.");
            }
            Check(row.RecalculatedPhysicalCubicMeters is > 0 and < 10000000000m, "volume fuori dai limiti SQL.");
        }
        var duplicateRows = report.Rows.GroupBy(SourceKey).Where(x => x.Count() > 1).Sum(x => x.Count() - 1);
        if (duplicateRows > 0) errors.Add("Identificativi duplicati ambigui nel workbook: correggere prima dell'import.");
        if (report.MissingFromAvailableSheet != 0 || report.ExtraInAvailableSheet != 0) errors.Add("Quadratura Magazzino / Materiale Disponibile non valida.");
        if (report.Rows.Where(x => !string.IsNullOrWhiteSpace(x.Qr)).GroupBy(x => LoadKey(x) + "|" + Normalize(x.Qr)).Any(x => x.Count() > 1))
            errors.Add("QR duplicati ambigui nello stesso carico.");
        foreach (var load in report.Rows.GroupBy(LoadKey))
            if (load.Select(x => (x.Date?.Date, x.Certification ?? "")).Distinct().Count() != 1)
                errors.Add($"Carico {load.First().LoadNumber}: date/certificazioni incoerenti.");
        foreach (var load in report.Rows.Where(x => x.Category == LegacyRowCategory.InitialInventory).GroupBy(LoadKey))
        {
            if (load.Sum(x => x.Pieces ?? 0) > int.MaxValue) errors.Add($"Carico {load.First().LoadNumber}: totale pezzi oltre i limiti SQL.");
            if (load.Any(x => Value(x) >= 1000000000000000m)) errors.Add($"Carico {load.First().LoadNumber}: valore oltre i limiti SQL.");
        }
        var keys = db.LegacyImportKeys.AsNoTracking().Select(x => x.ImportKey).ToHashSet(StringComparer.Ordinal);
        var known = report.Rows.Count(x => keys.Contains(SourceKey(x)));
        if (known > 0) warnings.Add($"{known} righe già note: saranno ignorate, senza sincronizzare né sovrascrivere lo stato SQL.");
        ValidateSqlCollisions(db, report.Rows.Where(x => !keys.Contains(SourceKey(x))).ToArray(), errors);
        return new(Path.GetFullPath(path), source.Length, File.GetLastWriteTimeUtc(path), hash, DateTime.UtcNow,
            report, errors.AsReadOnly(), warnings.AsReadOnly(), known, duplicateRows);
    }
    public LegacySqlImportResult Import(LegacySqlPreview preview, string operatorName)
    {
        if (!IsEnabled) throw new InvalidOperationException("Import iniziale disabilitato dall'ambiente.");
        if (string.IsNullOrWhiteSpace(operatorName) || operatorName.Length > 200) throw new InvalidOperationException("Indicare l'operatore dell'import (massimo 200 caratteri).");
        using var source = OpenSource(preview.FilePath); // Held until commit: no replace/write after verification.
        var current = AnalyzeLocked(preview.FilePath, source);
        if (current.Fingerprint != preview.Fingerprint) throw new InvalidOperationException("Il workbook è cambiato dopo la preview. Analizzare nuovamente il file.");
        if (!current.CanImport) throw new InvalidOperationException(string.Join(Environment.NewLine, current.BlockingErrors));
        using var strategyContext = factory.CreateDbContext();
        return strategyContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var db = factory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            db.Database.ExecuteSqlRaw("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'MagazzinoLegname.LegacyInitialImport', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @r<0 THROW 51000, 'Import legacy occupato: riprovare.', 1;");
            var existing = db.LegacyImportBatches.AsNoTracking().FirstOrDefault(x => x.FileFingerprint == current.Fingerprint);
            var known = db.LegacyImportKeys.AsNoTracking().Select(x => x.ImportKey).ToHashSet(StringComparer.Ordinal);
            var rows = current.Report.Rows.Where(x => !known.Contains(SourceKey(x))).ToArray();
            if (existing != null || rows.Length == 0) return new LegacySqlImportResult(existing?.Id, 0, 0, current.Report.TotalRows, 0, 0);
            var collisions = new List<string>(); ValidateSqlCollisions(db, rows, collisions);
            if (collisions.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, collisions));
            var inventory = rows.Where(x => x.Category == LegacyRowCategory.InitialInventory).ToArray();
            var history = rows.Where(x => x.Category == LegacyRowCategory.ClosedHistory).ToArray();
            var batch = new LegacyImportBatchEntity { Id = Guid.NewGuid(), Kind = LegacyImportKind.InitialInventory,
                FileName = Path.GetFileName(current.FilePath), FileFingerprint = current.Fingerprint, ImportedAtUtc = DateTime.UtcNow,
                ImportedBySnapshot = operatorName.Trim(), ImportedRows = rows.Length, LoadCount = rows.Select(LoadKey).Distinct().Count(),
                PackageCount = inventory.Length, PhysicalCubicMeters = inventory.Sum(Volume), LegacyAvailableCubicMeters = inventory.Sum(x => x.LegacyEstimatedCubicMeters ?? 0) };
            db.LegacyImportBatches.Add(batch);
            db.SaveChanges(); // Batch and all subsequent inserts remain inside this transaction.
            var suppliers = db.Suppliers.ToList();
            var families = db.ThicknessFamilies.AsNoTracking().ToArray();
            foreach (var loadRows in inventory.GroupBy(LoadKey))
            {
                var first = loadRows.First();
                var supplier = suppliers.SingleOrDefault(x => Normalize(x.Name) == Normalize(first.SupplierNormalized));
                if (supplier == null)
                {
                    supplier = new SupplierEntity { Id = Guid.NewGuid(), Name = first.SupplierNormalized!, Code = "L" + Guid.NewGuid().ToString("N")[..15], IsActive = false };
                    suppliers.Add(supplier); db.Suppliers.Add(supplier);
                }
                var parsed = LoadNumberSequenceService.TryParseLegacyLoadNumber(first.LoadNumber!, first.Date!.Value.Year, out var year, out var progressive);
                var load = new LoadEntity { Id = Guid.NewGuid(), SupplierId = supplier.Id, LoadNumber = first.LoadNumber!, LegacyLoadNumber = first.LoadNumber,
                    LoadYear = parsed ? year : null, AnnualProgressive = parsed ? progressive : null, Certification = first.Certification ?? "", ArrivalDate = first.Date.Value.Date,
                    LegacyImportBatchId = batch.Id, ExpectedPackages = loadRows.Count(), ReceiptOperatorSnapshot = "Import legacy · " + operatorName.Trim()[..Math.Min(operatorName.Trim().Length, 180)] };
                var sequence = 0;
                // Historical price is part of the group identity: no blending of different prices.
                foreach (var groupRows in loadRows.GroupBy(x => (x.InputThickness, x.InputWidth, x.InputLength, x.QualityNormalized, x.IsClassified, x.AppliedPrice)))
                {
                    var row = groupRows.First();
                    var group = new MaterialGroupEntity { Id = Guid.NewGuid(), LoadId = load.Id, IncomingThickness = row.InputThickness!.Value,
                        ConventionalThickness = families.Single(x => row.InputThickness >= x.MinimumIncomingThickness && row.InputThickness <= x.MaximumIncomingThickness).ConventionalThickness,
                        IncomingWidth = row.InputWidth!.Value, WidthAfterPlaning = row.InputWidth.Value, IncomingLength = row.InputLength!.Value,
                        Quality = row.QualityNormalized!, PackageCount = groupRows.Count(), InitialPieces = groupRows.Sum(x => decimal.ToInt32(x.Pieces!.Value)),
                        IncomingPhysicalCubicMeters = groupRows.Sum(Volume), AppliedPrice = row.AppliedPrice,
                        HistoricalValue = groupRows.Sum(Value), IsClassified = row.IsClassified == true, IsLegacyImport = true, WasteVerified = false,
                        LegacyEstimatedCubicMeters = groupRows.Sum(x => x.LegacyEstimatedCubicMeters ?? 0) };
                    load.MaterialGroups.Add(group);
                    foreach (var item in groupRows)
                    {
                        var code = "LEG-" + SourceKey(item)[10..];
                        load.Packages.Add(new PackageEntity { Id = Guid.NewGuid(), LoadId = load.Id, MaterialGroupId = group.Id, PackageCode = code,
                            QrPayload = item.Qr ?? code, PackageType = PersistentPackageType.Official, SequenceNumber = ++sequence, PieceCount = decimal.ToInt32(item.Pieces!.Value),
                            TotalOfficialPackages = item.TotalPackages ?? loadRows.Count(), Status = group.IsClassified ? "Classificato" : "Da classificare",
                            IncomingPhysicalCubicMeters = Volume(item), AppliedPrice = item.AppliedPrice, HistoricalPackageValue = Value(item), ArrivalDate = item.Date!.Value.Date,
                            LegacyPackageLabel = item.PackageLabel, LegacyExcelRow = item.ExcelRow, LegacyQr = item.Qr, LegacyImportBatchId = batch.Id });
                    }
                }
                db.Loads.Add(load);
            }
            foreach (var loadRows in history.GroupBy(LoadKey))
            {
                var historicalLoadId = Guid.NewGuid();
                foreach (var row in loadRows) db.LegacyHistoricalRecords.Add(new LegacyHistoricalRecordEntity { Id = Guid.NewGuid(), HistoricalLoadId = historicalLoadId,
                    BatchId = batch.Id, ImportKey = SourceKey(row), ExcelRow = row.ExcelRow, SupplierNameSnapshot = row.SupplierNormalized!, ArrivalDate = row.Date!.Value.Date,
                    LoadNumber = row.LoadNumber!, PackageLabel = row.PackageLabel, Pieces = row.Pieces!.Value, IncomingThickness = row.InputThickness!.Value,
                    IncomingWidth = row.InputWidth!.Value, IncomingLength = row.InputLength!.Value, QualityOriginal = row.QualityOriginal, QualityNormalized = row.QualityNormalized,
                    Certification = row.Certification, PhysicalCubicMeters = Volume(row), LegacyAvailableCubicMeters = LegacyMovementClassifier.IsSupplierReturn(row.FinishedRawValue) ? Volume(row) : row.LegacyEstimatedCubicMeters ?? row.ExcelCubicMeters ?? Volume(row),
                    LegacyEstimatedCubicMeters = row.LegacyEstimatedCubicMeters, IsClassified = row.IsClassified, ClassificationDate = row.ClassificationDate,
                    FinishedRawValue = row.FinishedRawValue, FinishedOn = row.FinishedOn, LegacyQr = row.Qr });
            }
            foreach (var row in rows) db.LegacyImportKeys.Add(new LegacyImportKeyEntity { Id = Guid.NewGuid(), BatchId = batch.Id, ImportKey = SourceKey(row), ExcelRow = row.ExcelRow });
            db.SaveChanges();
            // Reconcile persisted values before commit, including SQL decimal rounding.
            var saved = db.Packages.AsNoTracking().Where(x => x.LegacyImportBatchId == batch.Id).ToArray();
            var savedHistory = db.LegacyHistoricalRecords.AsNoTracking().Where(x => x.BatchId == batch.Id).ToArray();
            if (saved.Length != inventory.Length || savedHistory.Length != history.Length || saved.Sum(x => x.IncomingPhysicalCubicMeters) != inventory.Sum(Volume)
                || saved.Sum(x => x.HistoricalPackageValue ?? 0) != inventory.Sum(Value) || savedHistory.Sum(x => x.PhysicalCubicMeters) != history.Sum(Volume))
                throw new InvalidOperationException("Quadratura SQL/workbook non riuscita: rollback dell'intero batch.");
            transaction.Commit();
            return new LegacySqlImportResult(batch.Id, saved.Length, savedHistory.Length, current.Report.TotalRows - rows.Length,
                saved.Sum(x => x.IncomingPhysicalCubicMeters), saved.Sum(x => x.HistoricalPackageValue ?? 0));
        });
    }
    private static void ValidateSqlCollisions(MagazzinoDbContext db, IReadOnlyList<LegacyStagingRow> rows, List<string> errors)
    {
        var suppliers = db.Suppliers.AsNoTracking().ToArray();
        foreach (var name in rows.Select(x => x.SupplierNormalized).Distinct())
            if (suppliers.Count(x => Normalize(x.Name) == Normalize(name)) > 1) errors.Add($"Fornitore SQL ambiguo: {name}.");
        var loads = db.Loads.AsNoTracking().Include(x => x.Supplier).ToArray();
        foreach (var group in rows.Where(x => x.Category == LegacyRowCategory.InitialInventory).GroupBy(LoadKey))
        {
            var row = group.First();
            if (loads.Any(x => Normalize(x.Supplier.Name) == Normalize(row.SupplierNormalized) && Normalize(x.LoadNumber) == Normalize(row.LoadNumber) && x.ArrivalDate.Year == row.Date?.Year))
                errors.Add($"Carico {row.LoadNumber} già presente in SQL con righe nuove: import incrementale ambiguo, non sovrascrivere.");
        }
    }
    public static string Normalize(string? value) => string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    public static string LoadKey(LegacyStagingRow row) => $"{Normalize(row.SupplierNormalized)}|{row.Date?.Year}|{Normalize(row.LoadNumber)}";
    public static string SourceKey(LegacyStagingRow row)
    {
        var label = Regex.Replace(Normalize(row.PackageLabel), @"^PACCO\s+", "");
        var number = Regex.Match(label, @"^(\d+)\s+DI\s+\d+$");
        if (number.Success) label = number.Groups[1].Value.TrimStart('0');
        var identity = LoadKey(row) + "|" + (label.Length > 0 ? "LABEL:" + label : "QR:" + Normalize(row.Qr));
        return "TIMBER-V1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
    public static decimal Volume(LegacyStagingRow row) => decimal.Round(row.RecalculatedPhysicalCubicMeters ?? 0, 9, MidpointRounding.AwayFromZero);
    public static decimal Value(LegacyStagingRow row) => decimal.Round((row.RecalculatedPhysicalCubicMeters ?? 0) * (row.AppliedPrice ?? 0), 4, MidpointRounding.AwayFromZero);
}
