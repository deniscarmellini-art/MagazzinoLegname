using System.Collections.ObjectModel;
using System.ComponentModel;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Repositories;

namespace MagazzinoLegname.Services;

public sealed class OperatorCatalogService
{
    private static readonly Lazy<OperatorCatalogService> SharedInstance = new(() => new());
    private readonly ObservableCollection<string> _activeOperatorNames = [];
    private readonly IOperatorRepository _repository = SqlPersistenceRoot.Operators;
    private bool _isReloading;

    private OperatorCatalogService()
    {
        Operators = [];
        Reload();
    }

    public static OperatorCatalogService Shared => SharedInstance.Value;
    public ObservableCollection<Operator> Operators { get; }
    public ReadOnlyObservableCollection<string> ActiveOperatorNames { get; private set; } = null!;
    public event EventHandler? CatalogChanged;

    public Operator AddOperator()
    {
        Operator item;
        try { item = _repository.Add(); } catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
        item.PropertyChanged += Operator_PropertyChanged;
        Operators.Add(item);
        RefreshActiveOperators();
        return item;
    }

    public void ToggleActive(Operator item)
    {
        item.IsActive = !item.IsActive;
    }

    public void Reload()
    {
        try
        {
            _isReloading = true; foreach (var old in Operators) old.PropertyChanged -= Operator_PropertyChanged; Operators.Clear();
            foreach (var item in _repository.GetAll()) { item.PropertyChanged += Operator_PropertyChanged; Operators.Add(item); }
            RefreshActiveOperators();
        }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
        finally { _isReloading = false; }
    }

    private void Operator_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Operator.FirstName) or nameof(Operator.LastName) or nameof(Operator.IsActive))
        {
            if (!_isReloading && sender is Operator item)
                try { _repository.Save(item); } catch (Exception exception) { Reload(); throw SqlPersistenceRoot.OperatorException(exception); }
            RefreshActiveOperators();
        }
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
