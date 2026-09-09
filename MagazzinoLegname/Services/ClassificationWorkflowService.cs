using System.Collections.ObjectModel;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;

namespace MagazzinoLegname.Services;

public sealed class ClassificationWorkflowService
{
    public static ClassificationWorkflowService Shared { get; } = new();
    private ClassificationWorkflowService()
    {
        Loads = [];
        ReloadInboundLoads();
    }
    private readonly object _workflowLock = new();

    public ObservableCollection<ClassificationLoad> Loads { get; }
    public ObservableCollection<PhysicalPackageDraft> RegisteredPhysicalPackages { get; } = [];
    public ObservableCollection<SupplementaryPackage> SupplementaryPackages { get; } = [];
    public ObservableCollection<ClassificationMovement> ClassificationHistory { get; } = [];
    public ObservableCollection<WasteAdjustment> WasteAdjustmentHistory { get; } = [];
    public event EventHandler? WorkflowChanged;

    public void AddAdjustment(MaterialGroupClassification group, WasteAdjustment adjustment)
    {
        if (group.WasteVerified) return;
        WasteAdjustmentHistory.Add(adjustment);
        group.MarkWasteAsVerified();
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
        InventoryProjectionService.Shared.NotifyProjectionChanged();
    }

    public void NotifyClassificationChanged() => WorkflowChanged?.Invoke(this, EventArgs.Empty);

    public void RecordClassification(MaterialGroupClassification group)
    {
        if (!group.ClassificationDate.HasValue || string.IsNullOrWhiteSpace(group.ClassificationOperator)) return;
        ClassificationHistory.Add(new ClassificationMovement
        {
            LoadId = group.LoadId,
            MaterialGroupId = group.GroupId,
            ClassificationDate = group.ClassificationDate.Value,
            ClassificationOperator = group.ClassificationOperator
        });
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkOfficialLabelsPrinted(MaterialGroupClassification group, string operatorName, DateTime printedAt)
    {
        group.MarkOfficialLabelsPrinted(operatorName, printedAt);
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }

    public SupplementaryPackage CreateSupplementaryPackage(ClassificationLoad load, MaterialGroupClassification group,
        string operatorName, DateTime createdAt)
    {
        lock (_workflowLock)
        {
            if (group.IsClassified)
                throw new InvalidOperationException("Il gruppo è già classificato: non è possibile creare nuove etichette supplementari.");
            SupplementaryPackage package;
            try { package = SqlPersistenceRoot.InboundLoads.AddSupplementary(load, group, operatorName, createdAt); }
            catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
            SupplementaryPackages.Add(package);
            WorkflowChanged?.Invoke(this, EventArgs.Empty);
            return package;
        }
    }

    public void RegisterLoad(ClassificationLoad load, IReadOnlyList<PhysicalPackageDraft> packages)
    {
        if (Loads.Any(item => item.SupplierCode.Equals(load.SupplierCode, StringComparison.OrdinalIgnoreCase)
            && item.LoadNumber.Equals(load.LoadNumber, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Il numero carico è già presente per questo fornitore.");
        var existingCodes = RegisteredPhysicalPackages.Select(item => item.PackageCode)
            .Concat(SupplementaryPackages.Select(item => item.PackageCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (packages.Any(package => !existingCodes.Add(package.PackageCode)))
            throw new InvalidOperationException("È stato rilevato un CodicePacco duplicato.");
        Loads.Add(load);
        foreach (var package in packages) RegisteredPhysicalPackages.Add(package);
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RegisterPersistedLoad(ClassificationLoad load, IReadOnlyList<PhysicalPackageDraft> packages)
    {
        lock (_workflowLock)
        {
            Loads.Add(load);
            foreach (var package in packages) RegisteredPhysicalPackages.Add(package);
            WorkflowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ReloadInboundLoads()
    {
        lock (_workflowLock)
        {
            var persisted = SqlPersistenceRoot.InboundLoads.GetAll();
            Loads.Clear(); RegisteredPhysicalPackages.Clear(); SupplementaryPackages.Clear();
            foreach (var item in persisted)
            {
                Loads.Add(item.Load);
                foreach (var package in item.Packages) RegisteredPhysicalPackages.Add(package);
                foreach (var package in item.SupplementaryPackages) SupplementaryPackages.Add(package);
            }
            WorkflowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RegisterLegacyBatch(IReadOnlyList<ClassificationLoad> loads, IReadOnlyList<PhysicalPackageDraft> packages)
    {
        lock (_workflowLock)
        {
            var loadKeys = Loads.Select(x => $"{x.SupplierCode}|{x.LoadNumber}").ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (loads.Any(x => !loadKeys.Add($"{x.SupplierCode}|{x.LoadNumber}"))) throw new InvalidOperationException("Collisione con un numero carico esistente.");
            var codes = RegisteredPhysicalPackages.Select(x => x.PackageCode)
                .Concat(SupplementaryPackages.Select(x => x.PackageCode))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (packages.Any(x => !codes.Add(x.PackageCode))) throw new InvalidOperationException("Collisione con un codice pacco esistente.");
            foreach (var load in loads) Loads.Add(load);
            foreach (var package in packages) RegisteredPhysicalPackages.Add(package);
            WorkflowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RollbackLegacyBatch(Guid batchId)
    {
        lock (_workflowLock)
        {
            var loadIds = Loads.Where(x => x.LegacyImportBatchId == batchId).Select(x => x.Id).ToHashSet();
            foreach (var load in Loads.Where(x => loadIds.Contains(x.Id)).ToList()) Loads.Remove(load);
            foreach (var package in RegisteredPhysicalPackages.Where(x => loadIds.Contains(x.LoadId)).ToList()) RegisteredPhysicalPackages.Remove(package);
            WorkflowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ResetOperationalTestData()
    {
        lock (_workflowLock)
        {
            Loads.Clear(); RegisteredPhysicalPackages.Clear(); SupplementaryPackages.Clear(); ClassificationHistory.Clear(); WasteAdjustmentHistory.Clear();
            WorkflowChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
