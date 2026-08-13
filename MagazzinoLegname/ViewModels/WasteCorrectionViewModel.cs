using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class WasteCorrectionViewModel : ObservableObject
{
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;
    private WasteCorrectionRowViewModel? _selectedGroup;
    private string _selectedOperator;

    public WasteCorrectionViewModel()
    {
        Operators = ["Andrea Rossi", "Elena Bianchi", "Marco Conti"];
        _selectedOperator = Operators[0];
        _workflow.WorkflowChanged += (_, _) => ReloadEligibleGroups();
        ReloadEligibleGroups();
        if (Groups.Count > 0) { Groups[0].DiscardedWholeBoards = 18; Groups[0].PartialWastePercentage = 3.5m; }
    }

    public ObservableCollection<WasteCorrectionRowViewModel> Groups { get; } = [];
    public ObservableCollection<string> Operators { get; }
    public WasteCorrectionRowViewModel? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }
    public string SelectedOperator
    {
        get => _selectedOperator;
        set => SetProperty(ref _selectedOperator, value);
    }
    public string PendingCountText => Groups.Count == 1 ? "1 gruppo da verificare" : $"{Groups.Count} gruppi da verificare";

    public bool ConfirmSelected()
    {
        if (SelectedGroup is null || string.IsNullOrWhiteSpace(SelectedOperator)) return false;
        var row = SelectedGroup;
        _workflow.AddAdjustment(row.Group, row.CreateSnapshot(SelectedOperator, DateTime.Now));
        Groups.Remove(row);
        SelectedGroup = Groups.FirstOrDefault();
        OnPropertyChanged(nameof(PendingCountText));
        return true;
    }

    private void ReloadEligibleGroups()
    {
        Groups.Clear();
        foreach (var load in _workflow.Loads)
        foreach (var group in load.Groups.Where(group => group.IsClassified && !group.WasteVerified))
            Groups.Add(new WasteCorrectionRowViewModel(load, group));
        SelectedGroup = Groups.FirstOrDefault();
        OnPropertyChanged(nameof(PendingCountText));
    }
}
