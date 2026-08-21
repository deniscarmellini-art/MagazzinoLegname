using System.Collections.ObjectModel;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class InventoryProjectionService
{
    private readonly object _sync = new();
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;
    private readonly Dictionary<string, Guid> _packageIds = new(StringComparer.OrdinalIgnoreCase);

    public static InventoryProjectionService Shared { get; } = new();
    private InventoryProjectionService() { }

    public ObservableCollection<MaterialDischargeMovement> DischargeMovements { get; } = [];
    public ObservableCollection<ManualPackageRemovalMovement> ManualRemovalMovements { get; } = [];
    public event EventHandler? InventoryChanged;

    public IReadOnlyList<InventoryPackage> BuildInventory(bool includeDischarged = false)
    {
        lock (_sync) return BuildInventoryCore(includeDischarged);
    }

    public InventoryPackage? FindPackage(string packageCode)
    {
        lock (_sync)
            return BuildInventoryCore(true).FirstOrDefault(package =>
                package.PackageCode.Equals(packageCode, StringComparison.OrdinalIgnoreCase));
    }

    public MaterialDischargeMovement? FindMovement(string packageCode)
    {
        lock (_sync)
            return DischargeMovements.LastOrDefault(movement =>
                movement.PackageCode.Equals(packageCode, StringComparison.OrdinalIgnoreCase));
    }

    public MaterialDischargeMovement Discharge(string packageCode, string operatorName)
    {
        lock (_sync)
        {
            var package = BuildInventoryCore(true).FirstOrDefault(item =>
                item.PackageCode.Equals(packageCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Pacco non trovato.");
            if (!package.IsPresent) throw new InvalidOperationException("Pacco già scaricato.");
            if (package.ClassificationStatus != "Classificato")
                throw new InvalidOperationException("Il materiale deve essere classificato prima dello scarico.");
            if (!package.UsesRealCubicMeters)
                throw new InvalidOperationException("È necessario completare la rettifica scarti.");

            var movement = new MaterialDischargeMovement
            {
                PackageId = package.Id, PackageCode = package.PackageCode,
                LoadId = package.LoadId, MaterialGroupId = package.MaterialGroupId,
                LoadNumber = package.LoadNumber, SupplierName = package.SupplierName,
                DischargeDate = DateTime.Now, DischargeOperator = operatorName,
                DischargedCubicMeters = package.InventoryCubicMeters,
                PreviousStatus = "Presente", NextStatus = "Scaricato"
            };
            DischargeMovements.Add(movement);
            InventoryChanged?.Invoke(this, EventArgs.Empty);
            return movement;
        }
    }

    public ManualPackageRemovalMovement RemovePackage(string packageCode, string operatorName,
        string reason, string? note)
    {
        lock (_sync)
        {
            var package = BuildInventoryCore(false).FirstOrDefault(item =>
                item.PackageCode.Equals(packageCode, StringComparison.OrdinalIgnoreCase));
            if (package is null) throw new InvalidOperationException("Pacco non trovato o non più presente.");
            if (string.IsNullOrWhiteSpace(operatorName)) throw new InvalidOperationException("Indicare l'operatore.");
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Indicare il motivo della rimozione.");
            if (reason == "Altro" && string.IsNullOrWhiteSpace(note))
                throw new InvalidOperationException("Per il motivo Altro è richiesta una breve nota.");

            var movement = new ManualPackageRemovalMovement
            {
                PackageId = package.Id, PackageCode = package.PackageCode,
                LoadId = package.LoadId, MaterialGroupId = package.MaterialGroupId,
                LoadNumber = package.LoadNumber, SupplierName = package.SupplierName,
                RemovalDate = DateTime.Now, RemovalOperator = operatorName,
                RemovedCubicMeters = package.InventoryCubicMeters,
                Reason = reason, Note = note?.Trim() ?? string.Empty
            };
            ManualRemovalMovements.Add(movement);
            InventoryChanged?.Invoke(this, EventArgs.Empty);
            return movement;
        }
    }

    private IReadOnlyList<InventoryPackage> BuildInventoryCore(bool includeDischarged)
    {
        var packages = new List<InventoryPackage>();
        foreach (var load in _workflow.Loads)
        {
            var loadSequence = 1;
            foreach (var group in load.Groups)
            {
                var legacyPackages = group.IsLegacyImport
                    ? _workflow.RegisteredPhysicalPackages.Where(item => item.OriginGroupId == group.GroupId).OrderBy(item => item.SequenceNumber).ToList()
                    : [];
                var groupCodes = group.IsLegacyImport
                    ? legacyPackages.Select(item => item.PackageCode).ToList()
                    : Enumerable.Range(loadSequence, group.PackageCount).Select(sequence => $"{load.SupplierCode}-{load.LoadNumber}-P{sequence:00}").ToList();
                var movements = DischargeMovements.Where(item => item.MaterialGroupId == group.GroupId).ToList();
                var movementByCode = movements.ToDictionary(item => item.PackageCode, StringComparer.OrdinalIgnoreCase);
                var removals = ManualRemovalMovements.Where(item => item.MaterialGroupId == group.GroupId).ToList();
                var removalByCode = removals.ToDictionary(item => item.PackageCode, StringComparer.OrdinalIgnoreCase);
                var presentCodes = groupCodes.Where(code => !movementByCode.ContainsKey(code)
                    && !removalByCode.ContainsKey(code)).ToList();
                var adjustment = _workflow.WasteAdjustmentHistory.LastOrDefault(item => item.MaterialGroupId == group.GroupId);
                var originalGroupBalance = adjustment?.RealAvailableCubicMeters
                    ?? (group.IsLegacyImport ? group.LegacyEstimatedCubicMeters ?? 0m : group.TheoreticalUsefulCubicMeters ?? 0m);
                var residual = originalGroupBalance - movements.Sum(item => item.DischargedCubicMeters)
                    - removals.Sum(item => item.RemovedCubicMeters);
                if (presentCodes.Count == 0) residual = 0m;
                residual = Math.Max(0m, residual);
                Dictionary<string, decimal> shareByCode;
                if (group.IsLegacyImport && adjustment is null)
                    shareByCode = legacyPackages.Where(item => presentCodes.Contains(item.PackageCode, StringComparer.OrdinalIgnoreCase))
                        .ToDictionary(item => item.PackageCode, item => item.LegacyEstimatedCubicMeters ?? 0m, StringComparer.OrdinalIgnoreCase);
                else
                {
                    var currentShares = DistributeExactly(residual, presentCodes.Count);
                    shareByCode = presentCodes.Select((code, index) => (code, value: currentShares[index]))
                        .ToDictionary(item => item.code, item => item.value, StringComparer.OrdinalIgnoreCase);
                }
                var incomingShares = group.IsLegacyImport
                    ? legacyPackages.Select(item => item.IncomingPhysicalCubicMeters).ToArray()
                    : DistributeExactly(group.IncomingPhysicalCubicMeters, group.PackageCount).ToArray();

                for (var index = 0; index < group.PackageCount; index++, loadSequence++)
                {
                    var code = groupCodes[index];
                    var legacyPackage = group.IsLegacyImport ? legacyPackages[index] : null;
                    movementByCode.TryGetValue(code, out var movement);
                    removalByCode.TryGetValue(code, out var removal);
                    var isPresent = movement is null && removal is null;
                    if (!includeDischarged && !isPresent) continue;
                    if (!_packageIds.TryGetValue(code, out var packageId))
                        _packageIds[code] = packageId = Guid.NewGuid();
                    packages.Add(new InventoryPackage
                    {
                        Id = packageId, LoadId = load.Id, MaterialGroupId = group.GroupId,
                        PackageCode = code, LoadNumber = load.LoadNumber, SupplierName = load.SupplierName,
                        ArrivalDate = legacyPackage?.ArrivalDate ?? load.ArrivalDate, PackageNumber = legacyPackage?.LegacyPackageNumber ?? loadSequence,
                        TotalPackages = legacyPackage?.LegacyTotalPackages ?? load.TotalPackages,
                        ConventionalThickness = group.ConventionalThickness, IncomingThickness = group.IncomingThickness, WidthAfterPlaning = group.WidthAfterPlaning,
                        IncomingLength = group.IncomingLength, Quality = group.Quality, Certification = group.LegacyCertification ?? load.Certification,
                        ClassificationStatus = group.IsClassified ? "Classificato" : "Da classificare",
                        WasteAdjustmentStatus = adjustment is null ? "—" : "✓",
                        IncomingCubicMeters = incomingShares[index], ProcessingWastePercentage = group.ProcessingWastePercentage,
                        QualityWastePercentage = adjustment?.TotalClassificationWastePercentage,
                        InventoryCubicMeters = isPresent ? shareByCode[code] : 0m,
                        AppliedPrice = group.AppliedPrice, TheoreticalUsefulCubicMeters = group.TheoreticalUsefulCubicMeters,
                        LegacyEstimatedCubicMeters = legacyPackage?.LegacyEstimatedCubicMeters,
                        InventoryQuantitySource = adjustment is not null ? InventoryQuantitySource.RealAfterAdjustment
                            : group.IsLegacyImport ? InventoryQuantitySource.LegacyEstimate : InventoryQuantitySource.CurrentTheoretical,
                        LegacyLoadNumber = group.LegacyLoadNumber, LegacyPackageLabel = legacyPackage?.LegacyPackageLabel,
                        LegacyExcelRow = legacyPackage?.LegacyExcelRow, LegacyQr = legacyPackage?.LegacyQr, LegacyImportBatchId = group.LegacyImportBatchId,
                        UsesRealCubicMeters = adjustment is not null, IsPresent = isPresent,
                        PackageStatus = movement is not null ? "Scaricato" : removal is not null ? "Rimosso manualmente" : "Presente",
                        DischargeDate = movement?.DischargeDate, DischargeOperator = movement?.DischargeOperator,
                        DischargedCubicMeters = movement?.DischargedCubicMeters,
                        ManualRemovalDate = removal?.RemovalDate,
                        ManualRemovalOperator = removal?.RemovalOperator,
                        ManuallyRemovedCubicMeters = removal?.RemovedCubicMeters,
                        ManualRemovalReason = removal?.Reason,
                        ManualRemovalNote = removal?.Note,
                        WasteAdjustmentDate = adjustment?.AdjustmentDate,
                        WasteAdjustmentOperator = adjustment?.AdjustmentOperator
                    });
                }
            }
        }
        return packages;
    }

    public static IReadOnlyList<decimal> DistributeExactly(decimal total, int count)
    {
        if (count <= 0) return [];
        var result = new decimal[count];
        var regularShare = decimal.Round(total / count, 6, MidpointRounding.ToEven);
        for (var index = 0; index < count - 1; index++) result[index] = regularShare;
        result[count - 1] = total - result.Take(count - 1).Sum();
        return result;
    }

    public void ResetOperationalTestData()
    {
        lock (_sync)
        {
            DischargeMovements.Clear(); ManualRemovalMovements.Clear(); _packageIds.Clear();
            InventoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
