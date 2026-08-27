using System.Security.Cryptography;
using System.Text;
using System.IO;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class LegacyInitialInventoryImportService
{
    private readonly object _sync = new();
    private readonly List<LegacyImportBatch> _batches = [];
    private readonly HashSet<string> _importedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;
    private readonly SupplierCatalogService _catalog = SupplierCatalogService.Shared;
    public static LegacyInitialInventoryImportService Shared { get; } = new();
    private LegacyInitialInventoryImportService() { }
    public IReadOnlyList<LegacyImportBatch> Batches { get { lock (_sync) return _batches.ToArray(); } }

    public LegacyInitialInventoryImportPlan BuildPlan(LegacyImportReport report)
    {
        if (report.MissingFromAvailableSheet != 0 || report.ExtraInAvailableSheet != 0 || report.CurrentInventoryRows != report.AvailableSheetRows || report.MatchingAvailableRows != report.CurrentInventoryRows)
            throw new InvalidOperationException("La quadratura con Materiale Disponibile non è corretta.");
        var rows = report.Rows.Where(x => !x.IsExcluded && x.Category == LegacyRowCategory.InitialInventory).ToArray();
        if (report.Rows.Any(x => x.IsExcluded && x.Category == LegacyRowCategory.InitialInventory)) throw new InvalidOperationException("La giacenza contiene anomalie bloccanti.");
        if (rows.Any(x => x.QualityNormalized is not ("C" or "VISTA"))) throw new InvalidOperationException("La giacenza contiene qualità non importabili automaticamente.");
        var fingerprint = Fingerprint(report.FilePath);
        var collisions = new List<string>();
        lock (_sync)
        {
            if (_batches.Any(x => x.FileFingerprint == fingerprint)) collisions.Add("Questo file è già stato importato.");
            foreach (var row in rows.Where(x => _importedKeys.Contains(LegacyKey(fingerprint, x)))) collisions.Add($"Riga Excel {row.ExcelRow} già importata.");
        }
        var supplierNames = rows.Select(x => x.SupplierNormalized ?? x.SupplierOriginal!).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
        var reservedCodes = _catalog.Suppliers.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var suppliers = supplierNames.Select(name =>
        {
            var existing = _catalog.Suppliers.FirstOrDefault(x => x.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            return new LegacySupplierPlan(name, existing?.Code ?? CreateLegacyCode(name, reservedCodes), existing is not null);
        }).ToArray();
        var previews = BuildMaterialGroupPreviews(rows);
        return new() { BatchId = Guid.NewGuid(), FilePath = report.FilePath, FileFingerprint = fingerprint, Rows = rows, Suppliers = suppliers, Collisions = collisions, MaterialGroups = previews };
    }

    public LegacyInitialInventoryImportResult Commit(LegacyInitialInventoryImportPlan plan, string? operatorName)
    {
        lock (_sync)
        {
            if (!plan.CanCommit) throw new InvalidOperationException(string.Join(Environment.NewLine, plan.Collisions));
            if (_batches.Any(x => x.FileFingerprint == plan.FileFingerprint)) throw new InvalidOperationException("Importazione già applicata: fingerprint file già registrato.");
            if (plan.Rows.Any(x => _importedKeys.Contains(LegacyKey(plan.FileFingerprint, x)))) throw new InvalidOperationException("Importazione bloccata: almeno un pacco legacy risulta già registrato.");
            var createdSuppliers = new List<Supplier>();
            try
            {
                foreach (var supplierPlan in plan.Suppliers.Where(x => !x.Existing))
                {
                    var supplier = new Supplier(Guid.NewGuid(), supplierPlan.Name, false, supplierPlan.Code);
                    _catalog.Suppliers.Add(supplier); createdSuppliers.Add(supplier);
                }
                var supplierLookup = _catalog.Suppliers.ToDictionary(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase);
                var loads = new List<ClassificationLoad>(); var packages = new List<PhysicalPackageDraft>();
                foreach (var loadRows in plan.Rows.GroupBy(x => $"{x.SupplierNormalized ?? x.SupplierOriginal}|{x.LoadNumber}", StringComparer.OrdinalIgnoreCase))
                {
                    var first = loadRows.First(); var supplierName = first.SupplierNormalized ?? first.SupplierOriginal!; var supplier = supplierLookup[supplierName.Trim()]; var loadId = Guid.NewGuid();
                    var homogeneousRows = loadRows.GroupBy(row => new LegacyMaterialGroupKey(row.InputThickness!.Value, row.InputWidth!.Value,
                        row.InputLength!.Value, row.QualityNormalized!, row.IsClassified == true, row.Certification ?? string.Empty));
                    var groups = new List<MaterialGroupClassification>();
                    foreach (var homogeneousGroup in homogeneousRows)
                    {
                        var groupRows = homogeneousGroup.OrderBy(row => row.ExcelRow).ToArray();
                        var group = CreateGroup(plan, loadId, groupRows); groups.Add(group);
                        packages.AddRange(groupRows.Select(row => CreatePackage(plan, loadId, group.GroupId, row)));
                    }
                    var parsed = LoadNumberSequenceService.TryParseLegacyLoadNumber(first.LoadNumber!, first.Date!.Value.Year, out var loadYear, out var annualProgressive);
                    var load = new ClassificationLoad(groups) { Id = loadId, LoadNumber = first.LoadNumber!, LegacyLoadNumber = first.LoadNumber,
                        SupplierId = supplier.Id, LoadYear = parsed ? loadYear : null, AnnualProgressive = parsed ? annualProgressive : null,
                        LegacyLoadNumberParseWarning = parsed ? null : $"Numero carico legacy non interpretabile: {first.LoadNumber}",
                        LegacyImportBatchId = plan.BatchId, SupplierName = supplier.Name, SupplierCode = supplier.Code, Certification = first.Certification ?? string.Empty,
                        ArrivalDate = loadRows.Min(x => x.Date)!.Value.Date, ReceiptOperator = operatorName ?? "Importazione legacy" };
                    loads.Add(load);
                }
                _workflow.RegisterLegacyBatch(loads, packages);
                var batch = new LegacyImportBatch(plan.BatchId, Path.GetFileName(plan.FilePath), plan.FileFingerprint, DateTime.Now,
                    plan.Rows.Count, plan.LoadCount, plan.PackageCount, plan.PhysicalCubicMeters, plan.LegacyAvailableCubicMeters, operatorName);
                _batches.Add(batch); foreach (var row in plan.Rows) _importedKeys.Add(LegacyKey(plan.FileFingerprint, row)); _catalog.NotifyChanged();
                return new(batch, plan.PackageCount, plan.LoadCount, plan.Suppliers.Count(x => x.Existing), createdSuppliers.Count,
                    plan.ClassifiedCount, plan.ToClassifyCount, plan.PhysicalCubicMeters, plan.LegacyAvailableCubicMeters, plan.MissingPrices, 0);
            }
            catch
            {
                _workflow.RollbackLegacyBatch(plan.BatchId);
                foreach (var supplier in createdSuppliers) _catalog.Suppliers.Remove(supplier);
                throw;
            }
        }
    }

    private static MaterialGroupClassification CreateGroup(LegacyInitialInventoryImportPlan plan, Guid loadId, IReadOnlyList<LegacyStagingRow> rows)
    {
        var row = rows[0];
        var group = new MaterialGroupClassification { LoadId = loadId, IncomingThickness = row.InputThickness!.Value,
            ConventionalThickness = row.InputThickness.Value, UsefulThickness = 0m, IncomingWidth = row.InputWidth!.Value,
            WidthAfterPlaning = row.InputWidth.Value, FinalWidth = 0m, IncomingLength = row.InputLength!.Value, FinalLength = 0m,
            Quality = row.QualityNormalized!, PackageCount = rows.Count, InitialPieces = rows.Sum(item => decimal.ToInt32(item.Pieces!.Value)), AppliedPrice = null, LineValue = null,
            IsLegacyImport = true, WasClassifiedAtLegacyImport = row.IsClassified == true,
            LegacyEstimatedCubicMeters = rows.Sum(item => item.LegacyEstimatedCubicMeters ?? 0m), LegacyLoadNumber = row.LoadNumber,
            LegacyImportBatchId = plan.BatchId, LegacyCertification = row.Certification };
        if (row.IsClassified == true) group.MarkAsLegacyClassified(row.ClassificationDate);
        return group;
    }
    private static PhysicalPackageDraft CreatePackage(LegacyInitialInventoryImportPlan plan, Guid loadId, Guid groupId, LegacyStagingRow row) =>
        new(Guid.NewGuid(), loadId, groupId, row.ExcelRow, decimal.ToInt32(row.Pieces!.Value), row.InputThickness!.Value,
            row.InputWidth!.Value, row.InputWidth.Value, row.InputLength!.Value, row.QualityNormalized!)
        {
            TotalPackages = row.TotalPackages ?? 1, ArrivalDate = row.Date!.Value.Date,
            PackageCode = $"LEG-{plan.FileFingerprint[..10]}-{row.ExcelRow:000000}", QrPayload = row.Qr ?? string.Empty,
            Status = row.IsClassified == true ? "Classificato" : "Da classificare", LegacyPackageLabel = row.PackageLabel,
            LegacyExcelRow = row.ExcelRow, LegacyQr = row.Qr, LegacyIdentifier = LegacyKey(plan.FileFingerprint, row),
            LegacyEstimatedCubicMeters = row.LegacyEstimatedCubicMeters, LegacyImportBatchId = plan.BatchId,
            LegacyPackageNumber = row.PackageNumber, LegacyTotalPackages = row.TotalPackages
        };
    private sealed record LegacyMaterialGroupKey(decimal Thickness, decimal Width, decimal Length, string Quality, bool IsClassified, string Certification);
    private static IReadOnlyList<LegacyMaterialGroupPreview> BuildMaterialGroupPreviews(IReadOnlyList<LegacyStagingRow> rows)
    {
        var result = new List<LegacyMaterialGroupPreview>();
        foreach (var loadRows in rows.GroupBy(x => $"{x.SupplierNormalized ?? x.SupplierOriginal}|{x.LoadNumber}", StringComparer.OrdinalIgnoreCase))
        {
            var groupNumber = 0;
            var groups = loadRows.GroupBy(row => new LegacyMaterialGroupKey(row.InputThickness!.Value, row.InputWidth!.Value,
                row.InputLength!.Value, row.QualityNormalized!, row.IsClassified == true, row.Certification ?? string.Empty));
            foreach (var group in groups)
            {
                var materialRows = group.ToArray(); groupNumber++;
                result.Add(new(materialRows[0].LoadNumber!, groupNumber, group.Key.Thickness, group.Key.Width, group.Key.Length,
                    group.Key.Quality, group.Key.IsClassified ? "Classificato" : "Da classificare", group.Key.Certification,
                    materialRows.Length, materialRows.Sum(x => decimal.ToInt32(x.Pieces!.Value)),
                    materialRows.Sum(x => x.RecalculatedPhysicalCubicMeters ?? 0m), materialRows.Sum(x => x.LegacyEstimatedCubicMeters ?? 0m)));
            }
        }
        return result;
    }
    private static string Fingerprint(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static string LegacyKey(string fingerprint, LegacyStagingRow row) => $"{fingerprint}|{row.ExcelRow}|{row.LoadNumber}|{row.PackageLabel}";
    private static string CreateLegacyCode(string name, HashSet<string> reserved)
    {
        var stem = "L" + new string(name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).Take(5).ToArray()); var code = stem; var suffix = 1;
        while (!reserved.Add(code)) code = stem[..Math.Min(stem.Length, 6)] + suffix++;
        return code;
    }
    public void ResetTestImportRegistry()
    {
        lock (_sync) { _batches.Clear(); _importedKeys.Clear(); }
    }
}
