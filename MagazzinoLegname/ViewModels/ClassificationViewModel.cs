using System.Collections.ObjectModel;
using System.ComponentModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class ClassificationViewModel : ObservableObject
{
    private readonly IReadOnlyList<ClassificationLoad> _allLoads;
    private readonly HashSet<Guid> _subscribedLoadIds = [];
    private ClassificationLoad? _selectedLoad;

    public ClassificationViewModel()
    {
        _allLoads = ClassificationWorkflowService.Shared.Loads;
        Operators = ["Andrea Rossi", "Elena Bianchi", "Marco Conti"];
        SubscribeToNewLoads();
        ClassificationWorkflowService.Shared.WorkflowChanged += (_, _) =>
        {
            SubscribeToNewLoads();
            ApplyFilters();
        };
        ApplyFilters();
    }

    public ObservableCollection<ClassificationLoad> VisibleLoads { get; } = [];
    public ObservableCollection<string> Operators { get; }
    public ClassificationLoad? SelectedLoad
    {
        get => _selectedLoad;
        set => SetProperty(ref _selectedLoad, value);
    }
    public string LoadCountText => VisibleLoads.Count == 1
        ? "1 carico da completare" : $"{VisibleLoads.Count} carichi da completare";

    public void MarkGroupAsClassified(MaterialGroupClassification group)
    {
        var load = _allLoads.FirstOrDefault(item => item.Id == group.LoadId);
        if (group.IsClassified || load is null || string.IsNullOrWhiteSpace(load.SelectedOperator)) return;
        group.MarkAsClassified(load.SelectedOperator, DateTime.Now);
        ClassificationWorkflowService.Shared.NotifyClassificationChanged();
    }

    public void UndoGroupClassification(MaterialGroupClassification group)
    {
        if (!group.UndoClassification()) return;
        ClassificationWorkflowService.Shared.NotifyClassificationChanged();
    }

    private void ApplyFilters()
    {
        var previousSelection = SelectedLoad;
        var matches = _allLoads.Where(load => !load.IsFullyClassified)
            .ToList();
        VisibleLoads.Clear();
        foreach (var load in matches) VisibleLoads.Add(load);
        SelectedLoad = previousSelection is not null && matches.Contains(previousSelection)
            ? previousSelection : matches.FirstOrDefault();
        OnPropertyChanged(nameof(LoadCountText));
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
