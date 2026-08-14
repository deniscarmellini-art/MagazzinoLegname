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
                var groupCodes = Enumerable.Range(loadSequence, group.PackageCount)
                    .Select(sequence => $"{load.SupplierCode}-{load.LoadNumber}-P{sequence:00}").ToList();
                var movements = DischargeMovements.Where(item => item.MaterialGroupId == group.GroupId).ToList();
                var movementByCode = movements.ToDictionary(item => item.PackageCode, StringComparer.OrdinalIgnoreCase);
                var removals = ManualRemovalMovements.Where(item => item.MaterialGroupId == group.GroupId).ToList();
                var removalByCode = removals.ToDictionary(item => item.PackageCode, StringComparer.OrdinalIgnoreCase);
                var presentCodes = groupCodes.Where(code => !movementByCode.ContainsKey(code)
                    && !removalByCode.ContainsKey(code)).ToList();
                var adjustment = _workflow.WasteAdjustmentHistory.LastOrDefault(item => item.MaterialGroupId == group.GroupId);
                var originalGroupBalance = adjustment?.RealAvailableCubicMeters ?? group.TheoreticalUsefulCubicMeters;
                var residual = originalGroupBalance - movements.Sum(item => item.DischargedCubicMeters)
                    - removals.Sum(item => item.RemovedCubicMeters);
                if (presentCodes.Count == 0) residual = 0m;
                residual = Math.Max(0m, residual);
                var currentShares = DistributeExactly(residual, presentCodes.Count);
                var shareByCode = presentCodes.Select((code, index) => (code, value: currentShares[index]))
                    .ToDictionary(item => item.code, item => item.value, StringComparer.OrdinalIgnoreCase);
                var incomingShares = DistributeExactly(group.IncomingPhysicalCubicMeters, group.PackageCount);

                for (var index = 0; index < group.PackageCount; index++, loadSequence++)
                {
                    var code = groupCodes[index];
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
                        ArrivalDate = load.ArrivalDate, PackageNumber = loadSequence, TotalPackages = load.TotalPackages,
                        ConventionalThickness = group.ConventionalThickness, WidthAfterPlaning = group.WidthAfterPlaning,
                        IncomingLength = group.IncomingLength, Quality = group.Quality, Certification = load.Certification,
                        ClassificationStatus = group.IsClassified ? "Classificato" : "Da classificare",
                        WasteAdjustmentStatus = adjustment is null ? "—" : "✓",
                        IncomingCubicMeters = incomingShares[index], ProcessingWastePercentage = group.ProcessingWastePercentage,
                        QualityWastePercentage = adjustment?.TotalClassificationWastePercentage,
                        InventoryCubicMeters = isPresent ? shareByCode[code] : 0m,
                        AppliedPrice = group.AppliedPrice,
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
}
