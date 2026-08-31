using System.Collections.ObjectModel;
using System.ComponentModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class ClassificationViewModel : ObservableObject
{
    private readonly IReadOnlyList<ClassificationLoad> _allLoads;
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;
    private readonly InventoryProjectionService _inventory = InventoryProjectionService.Shared;
    private readonly HashSet<Guid> _subscribedLoadIds = [];
    private ClassificationLoad? _selectedLoad;

    public ClassificationViewModel()
    {
        _allLoads = _workflow.Loads;
        Operators = OperatorCatalogService.Shared.ActiveOperatorNames;
        OperatorCatalogService.Shared.CatalogChanged += (_, _) => EnsureActiveSelections();
        SubscribeToNewLoads();
        _workflow.WorkflowChanged += (_, _) =>
        {
            SubscribeToNewLoads();
            ApplyFilters();
        };
        _inventory.InventoryChanged += (_, _) => ApplyFilters();
        ApplyFilters();
    }

    public ObservableCollection<ClassificationLoad> VisibleLoads { get; } = [];
    public ObservableCollection<MaterialGroupClassification> OperationalGroups { get; } = [];
    public ReadOnlyObservableCollection<string> Operators { get; }
    public ClassificationLoad? SelectedLoad
    {
        get => _selectedLoad;
        set
        {
            if (SetProperty(ref _selectedLoad, value)) RefreshOperationalGroups();
        }
    }
    public string LoadCountText => VisibleLoads.Count == 1
        ? "1 carico da completare" : $"{VisibleLoads.Count} carichi da completare";

    public IReadOnlyList<PhysicalPackageDraft> GetOfficialPackages(MaterialGroupClassification group) =>
        _workflow.RegisteredPhysicalPackages
            .Where(package => package.OriginGroupId == group.GroupId && package.PackageType == PackageType.Official)
            .OrderBy(package => package.SequenceNumber)
            .ToArray();

    public IReadOnlyList<PhysicalPackageDraft> GetSupplementaryPackages(MaterialGroupClassification group) =>
        _workflow.SupplementaryPackages
            .Where(package => package.MaterialGroupId == group.GroupId)
            .OrderBy(package => package.SupplementarySequence)
            .Select(ToLabelDraft)
            .ToArray();

    public PhysicalPackageDraft CreateSupplementaryPackage(MaterialGroupClassification group)
    {
        var load = _allLoads.FirstOrDefault(item => item.Id == group.LoadId)
            ?? throw new InvalidOperationException("Carico non trovato.");
        if (string.IsNullOrWhiteSpace(load.SelectedOperator))
            throw new InvalidOperationException("Selezionare un operatore prima di creare l'etichetta supplementare.");
        return ToLabelDraft(_workflow.CreateSupplementaryPackage(load, group, load.SelectedOperator, DateTime.Now));
    }

    public void MarkOfficialLabelsPrinted(MaterialGroupClassification group)
    {
        var load = _allLoads.FirstOrDefault(item => item.Id == group.LoadId)
            ?? throw new InvalidOperationException("Carico non trovato.");
        if (string.IsNullOrWhiteSpace(load.SelectedOperator))
            throw new InvalidOperationException("Selezionare un operatore prima della stampa.");
        _workflow.MarkOfficialLabelsPrinted(group, load.SelectedOperator, DateTime.Now);
    }

    public string GetSupplierName(MaterialGroupClassification group) =>
        _allLoads.FirstOrDefault(item => item.Id == group.LoadId)?.SupplierName ?? string.Empty;

    public string GetLoadNumber(MaterialGroupClassification group) =>
        _allLoads.FirstOrDefault(item => item.Id == group.LoadId)?.LoadNumber ?? string.Empty;

    public string GetDeliveryNoteNumber(MaterialGroupClassification group) =>
        _allLoads.FirstOrDefault(item => item.Id == group.LoadId)?.DeliveryNoteNumber ?? string.Empty;

    public string GetCertification(MaterialGroupClassification group) =>
        _allLoads.FirstOrDefault(item => item.Id == group.LoadId)?.Certification ?? string.Empty;

    public string GetPackageSummary(MaterialGroupClassification group)
    {
        var supplementary = _workflow.SupplementaryPackages.Count(item => item.MaterialGroupId == group.GroupId);
        return $"Ufficiali: {group.PackageCount} · Supplementari: {supplementary} · Fisici tracciati: {group.PackageCount + supplementary}";
    }

    public void MarkGroupAsClassified(MaterialGroupClassification group)
    {
        var load = _allLoads.FirstOrDefault(item => item.Id == group.LoadId);
        if (group.IsClassified || load is null || string.IsNullOrWhiteSpace(load.SelectedOperator)) return;
        group.MarkAsClassified(load.SelectedOperator, DateTime.Now);
        _workflow.RecordClassification(group);
    }

    public void UndoGroupClassification(MaterialGroupClassification group)
    {
        if (!group.UndoClassification()) return;
        _workflow.NotifyClassificationChanged();
    }

    private static PhysicalPackageDraft ToLabelDraft(SupplementaryPackage package) => new(
        package.Id, package.LoadId, package.MaterialGroupId, package.SupplementarySequence, 0,
        package.IncomingThickness, package.IncomingWidth, package.WidthAfterPlaning,
        package.IncomingLength, package.Quality)
    {
        TotalPackages = 0,
        ArrivalDate = package.ArrivalDate,
        PackageCode = package.PackageCode,
        QrPayload = package.QrPayload,
        PackageType = PackageType.Supplementary,
        SupplementarySequence = package.SupplementarySequence
    };

    private void ApplyFilters()
    {
        var previousSelection = SelectedLoad;
        var presentGroupIds = _inventory.BuildInventory().Select(package => package.MaterialGroupId).ToHashSet();
        var matches = _allLoads.Where(load => load.Groups.Any(group =>
                presentGroupIds.Contains(group.GroupId) && !group.IsClassified))
            .ToList();
        VisibleLoads.Clear();
        foreach (var load in matches) VisibleLoads.Add(load);
        SelectedLoad = previousSelection is not null && matches.Contains(previousSelection)
            ? previousSelection : matches.FirstOrDefault();
        RefreshOperationalGroups();
        OnPropertyChanged(nameof(LoadCountText));
        EnsureActiveSelections();
    }

    private void RefreshOperationalGroups()
    {
        var presentGroupIds = _inventory.BuildInventory().Select(package => package.MaterialGroupId).ToHashSet();
        OperationalGroups.Clear();
        if (SelectedLoad is null) return;
        foreach (var group in SelectedLoad.Groups.Where(group => presentGroupIds.Contains(group.GroupId)))
            OperationalGroups.Add(group);
    }

    private void EnsureActiveSelections()
    {
        var fallback = Operators.FirstOrDefault() ?? string.Empty;
        foreach (var load in VisibleLoads.Where(load => !Operators.Contains(load.SelectedOperator)))
            load.SelectedOperator = fallback;
    }

    private void Load_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClassificationLoad.Status) or nameof(ClassificationLoad.IsFullyClassified))
            ApplyFilters();
    }

    private void SubscribeToNewLoads()
    {
        foreach (var load in _allLoads.Where(load => _subscribedLoadIds.Add(load.Id)))
            load.PropertyChanged += Load_PropertyChanged;
    }
}