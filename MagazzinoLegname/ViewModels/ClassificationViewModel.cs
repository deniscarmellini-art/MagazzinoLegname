using System.Collections.ObjectModel;
using System.ComponentModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class ClassificationViewModel : ObservableObject
{
    private readonly IReadOnlyList<ClassificationLoad> _allLoads;
    private readonly InventoryProjectionService _inventory = InventoryProjectionService.Shared;
    private readonly HashSet<Guid> _subscribedLoadIds = [];
    private ClassificationLoad? _selectedLoad;

    public ClassificationViewModel()
    {
        _allLoads = ClassificationWorkflowService.Shared.Loads;
        Operators = OperatorCatalogService.Shared.ActiveOperatorNames;
        OperatorCatalogService.Shared.CatalogChanged += (_, _) => EnsureActiveSelections();
        SubscribeToNewLoads();
        ClassificationWorkflowService.Shared.WorkflowChanged += (_, _) =>
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

    public void MarkGroupAsClassified(MaterialGroupClassification group)
    {
        var load = _allLoads.FirstOrDefault(item => item.Id == group.LoadId);
        if (group.IsClassified || load is null || string.IsNullOrWhiteSpace(load.SelectedOperator)) return;
        group.MarkAsClassified(load.SelectedOperator, DateTime.Now);
        ClassificationWorkflowService.Shared.RecordClassification(group);
    }

    public void UndoGroupClassification(MaterialGroupClassification group)
    {
        if (!group.UndoClassification()) return;
        ClassificationWorkflowService.Shared.NotifyClassificationChanged();
    }

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
