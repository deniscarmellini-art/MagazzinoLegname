using System.Collections.ObjectModel;
using System.ComponentModel;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class OperatorCatalogService
{
    private static readonly Lazy<OperatorCatalogService> SharedInstance = new(() => new());
    private readonly ObservableCollection<string> _activeOperatorNames = [];

    private OperatorCatalogService()
    {
        Operators =
        [
            Create("Andrea", "Rossi"),
            Create("Elena", "Bianchi"),
            Create("Marco", "Conti")
        ];
        foreach (var item in Operators) item.PropertyChanged += Operator_PropertyChanged;
        RefreshActiveOperators();
    }

    public static OperatorCatalogService Shared => SharedInstance.Value;
    public ObservableCollection<Operator> Operators { get; }
    public ReadOnlyObservableCollection<string> ActiveOperatorNames { get; private set; } = null!;
    public event EventHandler? CatalogChanged;

    public Operator AddOperator()
    {
        var item = Create("Nuovo", "Operatore");
        item.PropertyChanged += Operator_PropertyChanged;
        Operators.Add(item);
        RefreshActiveOperators();
        return item;
    }

    public void ToggleActive(Operator item)
    {
        item.IsActive = !item.IsActive;
    }

    private static Operator Create(string firstName, string lastName) =>
        new() { FirstName = firstName, LastName = lastName, IsActive = true };

    private void Operator_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Operator.FirstName) or nameof(Operator.LastName) or nameof(Operator.IsActive))
            RefreshActiveOperators();
    }

    private void RefreshActiveOperators()
    {
        if (ActiveOperatorNames is null)
            ActiveOperatorNames = new ReadOnlyObservableCollection<string>(_activeOperatorNames);
        _activeOperatorNames.Clear();
        foreach (var name in Operators.Where(item => item.IsActive)
                     .Select(item => item.DisplayName)
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
            _activeOperatorNames.Add(name);
        CatalogChanged?.Invoke(this, EventArgs.Empty);
    }
}
