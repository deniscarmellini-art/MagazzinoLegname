using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class WasteCorrectionViewModel : ObservableObject
{
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;
    private WasteCorrectionRowViewModel? _selectedGroup;

    public WasteCorrectionViewModel()
    {
        Operators = OperatorCatalogService.Shared.ActiveOperatorNames;
        OperatorCatalogService.Shared.CatalogChanged += (_, _) => EnsureActiveSelections();
        _workflow.WorkflowChanged += (_, _) => ReloadEligibleGroups();
        ReloadEligibleGroups();
        if (Groups.Count > 0) { Groups[0].DiscardedWholeBoards = 18; Groups[0].PartialWastePercentage = 3.5m; }
    }

    public ObservableCollection<WasteCorrectionRowViewModel> Groups { get; } = [];
    public ReadOnlyObservableCollection<string> Operators { get; }
    public WasteCorrectionRowViewModel? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }
    public bool ConfirmSelected()
    {
        if (SelectedGroup is null || string.IsNullOrWhiteSpace(SelectedGroup.SelectedOperator)) return false;
        var row = SelectedGroup;
        _workflow.AddAdjustment(row.Group, row.CreateSnapshot(row.SelectedOperator, DateTime.Now));
        Groups.Remove(row);
        SelectedGroup = Groups.FirstOrDefault();
        return true;
    }

    private void ReloadEligibleGroups()
    {
        Groups.Clear();
        foreach (var load in _workflow.Loads)
        foreach (var group in load.Groups.Where(group => group.IsClassified && !group.WasteVerified))
            Groups.Add(new WasteCorrectionRowViewModel(load, group) { SelectedOperator = Operators.FirstOrDefault() ?? string.Empty });
        SelectedGroup = Groups.FirstOrDefault();
    }

    private void EnsureActiveSelections()
    {
        var fallback = Operators.FirstOrDefault() ?? string.Empty;
        foreach (var row in Groups.Where(row => !Operators.Contains(row.SelectedOperator)))
            row.SelectedOperator = fallback;
    }
}
