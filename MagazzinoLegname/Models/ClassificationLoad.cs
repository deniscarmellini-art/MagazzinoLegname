using System.Collections.ObjectModel;
using System.ComponentModel;
using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class ClassificationLoad : ObservableObject
{
    private string _selectedOperator = "Andrea Rossi";
    public ClassificationLoad(IEnumerable<MaterialGroupClassification> groups)
    {
        Groups = new(groups);
        foreach (var group in Groups) group.PropertyChanged += Group_PropertyChanged;
    }

    public Guid Id { get; init; } = Guid.NewGuid();
    public required string LoadNumber { get; init; }
    public required string SupplierName { get; init; }
    public required string SupplierCode { get; init; }
    public string Certification { get; init; } = "PEFC";
    public DateTime ArrivalDate { get; init; }
    public ObservableCollection<MaterialGroupClassification> Groups { get; }
    public string SelectedOperator
    {
        get => _selectedOperator;
        set => SetProperty(ref _selectedOperator, value);
    }
    public int TotalPackages => Groups.Sum(group => group.PackageCount);
    public int GroupsToClassify => Groups.Count(group => !group.IsClassified);
    public int ClassifiedGroups => Groups.Count(group => group.IsClassified);
    public bool IsFullyClassified => Groups.Count > 0 && Groups.All(group => group.IsClassified);
    public string Status => ClassifiedGroups switch
    {
        0 => "Da classificare",
        _ when IsFullyClassified => "Classificato",
        _ => "Parzialmente classificato"
    };

    private void Group_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(MaterialGroupClassification.IsClassified)
            or nameof(MaterialGroupClassification.ClassificationStatus))) return;
        OnPropertyChanged(nameof(GroupsToClassify));
        OnPropertyChanged(nameof(ClassifiedGroups));
        OnPropertyChanged(nameof(IsFullyClassified));
        OnPropertyChanged(nameof(Status));
    }
}
