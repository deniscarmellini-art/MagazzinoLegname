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
        try
        {
            var operatorItem = ResolveOperator(adjustment.AdjustmentOperator);
            var persisted = SqlPersistenceRoot.WasteAdjustments.Add(group.LoadId, group.GroupId,
                group.RowVersion.ToArray(), adjustment, operatorItem);
            WasteAdjustmentHistory.Add(persisted.Adjustment);
            group.ApplyPersistedWasteVerification(persisted.MaterialGroupRowVersion);
        }
        catch (Exception exception)
        {
            if (DatabaseErrorTranslator.Translate(exception).Kind is DatabaseFailureKind.ConcurrencyConflict
                or DatabaseFailureKind.UniqueConstraint)
                ReloadInboundLoads();
            throw SqlPersistenceRoot.OperatorException(exception);
        }
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
        InventoryProjectionService.Shared.NotifyProjectionChanged();
    }

    public void MarkClassified(Guid loadId, Guid materialGroupId, byte[] rowVersion,
        MaterialGroupClassification target, string operatorName, DateTime classifiedAt)
    {
        var operatorItem = ResolveOperator(operatorName);
        try
        {
            var state = SqlPersistenceRoot.Classifications.MarkClassified(loadId, materialGroupId, rowVersion,
                operatorItem, classifiedAt);
            target.ApplyPersistedClassification(state.RowVersion, state.IsClassified, state.ClassificationDate,
                state.ClassificationOperator, state.OfficialLabelsPrintedAt, state.OfficialLabelsPrintedBy);
            ClassificationHistory.Add(new ClassificationMovement { LoadId = loadId,
                MaterialGroupId = materialGroupId, ClassificationDate = state.ClassificationDate ?? classifiedAt,
                ClassificationOperator = state.ClassificationOperator ?? operatorName });
        }
        catch (Exception exception) { HandleClassificationFailure(exception); }
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkOfficialLabelsPrinted(Guid loadId, Guid materialGroupId, byte[] rowVersion,
        MaterialGroupClassification target, string operatorName, DateTime printedAt)
    {
        try
        {
            var state = SqlPersistenceRoot.Classifications.MarkOfficialLabelsPrinted(loadId, materialGroupId,
                rowVersion, ResolveOperator(operatorName), printedAt);
            target.ApplyPersistedClassification(state.RowVersion, state.IsClassified, state.ClassificationDate,
                state.ClassificationOperator, state.OfficialLabelsPrintedAt, state.OfficialLabelsPrintedBy);
        }
        catch (Exception exception) { HandleClassificationFailure(exception); }
        WorkflowChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UndoClassification(Guid loadId, Guid materialGroupId, byte[] rowVersion,
        MaterialGroupClassification target)
    {
        try
        {
            var state = SqlPersistenceRoot.Classifications.UndoClassification(loadId, materialGroupId, rowVersion);
            target.ApplyPersistedClassification(state.RowVersion, state.IsClassified, state.ClassificationDate,
                state.ClassificationOperator, state.OfficialLabelsPrintedAt, state.OfficialLabelsPrintedBy);
        }
        catch (Exception exception) { HandleClassificationFailure(exception); }
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
            WasteAdjustmentHistory.Clear();
            foreach (var item in persisted)
            {
                Loads.Add(item.Load);
                foreach (var package in item.Packages) RegisteredPhysicalPackages.Add(package);
                foreach (var package in item.SupplementaryPackages) SupplementaryPackages.Add(package);
                foreach (var adjustment in item.WasteAdjustments) WasteAdjustmentHistory.Add(adjustment);
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

    private static Operator ResolveOperator(string operatorName) =>
        OperatorCatalogService.Shared.Operators.SingleOrDefault(x => x.IsActive &&
            x.DisplayName.Equals(operatorName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("L'operatore selezionato non è disponibile nel database.");

    private void HandleClassificationFailure(Exception exception)
    {
        if (DatabaseErrorTranslator.Translate(exception).Kind == DatabaseFailureKind.ConcurrencyConflict)
            ReloadInboundLoads();
        throw SqlPersistenceRoot.OperatorException(exception);
    }
}
