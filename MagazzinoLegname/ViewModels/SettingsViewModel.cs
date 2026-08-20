using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SupplierCatalogService _catalog = SupplierCatalogService.Shared;
    private Supplier? _selectedSupplier;
    private SupplierConfigurationRowViewModel? _selectedPriceEditor;
    private bool _isHistoryVisible;
    private string _selectedSection = "Suppliers";
    private SupplierContact? _selectedContact;

    public SettingsViewModel()
    {
        Suppliers = _catalog.Suppliers;
        _selectedSupplier = Suppliers.FirstOrDefault();
        _catalog.CatalogChanged += (_, _) => Refresh();
        Refresh();
    }
    public ObservableCollection<Supplier> Suppliers { get; }
    public ObservableCollection<SupplierConfigurationRowViewModel> Configurations { get; } = [];
    public ObservableCollection<SupplierPrice> PriceHistory { get; } = [];
    public MaterialParameters MaterialParameters { get; } = MaterialParametersService.Shared.Parameters;
    public GeneralSettings GeneralSettings { get; } = GeneralSettingsService.Shared.Settings;
    public PlanningSettings PlanningSettings { get; } = PlanningSettingsService.Shared.Settings;
    public ObservableCollection<Operator> Operators => OperatorCatalogService.Shared.Operators;
    public string SelectedSection { get => _selectedSection; set { if (SetProperty(ref _selectedSection, value)) { OnPropertyChanged(nameof(IsSuppliersSection)); OnPropertyChanged(nameof(IsMaterialParametersSection)); OnPropertyChanged(nameof(IsPlanningParametersSection)); OnPropertyChanged(nameof(IsOperatorsSection)); } } }
    public bool IsSuppliersSection => SelectedSection == "Suppliers";
    public bool IsMaterialParametersSection => SelectedSection == "MaterialParameters";
    public bool IsPlanningParametersSection => SelectedSection == "PlanningParameters";
    public bool IsOperatorsSection => SelectedSection == "Operators";
    public SupplierContact? SelectedContact { get => _selectedContact; set => SetProperty(ref _selectedContact, value); }
    public Supplier? SelectedSupplier
    {
        get => _selectedSupplier;
        set { if (SetProperty(ref _selectedSupplier, value)) Refresh(); }
    }
    public bool IsHistoryVisible { get => _isHistoryVisible; set => SetProperty(ref _isHistoryVisible, value); }
    public Supplier AddSupplier() { var supplier = _catalog.AddSupplier("Nuovo fornitore"); SelectedSupplier = supplier; return supplier; }
    public void SaveSupplier()
    {
        if (SelectedSupplier is null) return;
        if (string.IsNullOrWhiteSpace(SelectedSupplier.Code))
            throw new InvalidOperationException("Il codice fornitore è obbligatorio.");
        if (!_catalog.IsSupplierCodeUnique(SelectedSupplier))
            throw new InvalidOperationException("Il codice fornitore deve essere univoco.");
        _catalog.NotifyChanged();
    }
    public void ShowSuppliers() => SelectedSection = "Suppliers";
    public void ShowMaterialParameters() { IsHistoryVisible = false; SelectedSection = "MaterialParameters"; }
    public void ShowPlanningParameters() { IsHistoryVisible = false; SelectedSection = "PlanningParameters"; }
    public void ShowOperators() { IsHistoryVisible = false; SelectedSection = "Operators"; }
    public void SaveMaterialParameters()
    {
        MaterialParametersService.Shared.NotifyChanged();
        GeneralSettingsService.Shared.NotifyChanged();
    }
    public void SavePlanningSettings() => PlanningSettingsService.Shared.NotifyChanged();
    public Operator AddOperator() => OperatorCatalogService.Shared.AddOperator();
    public void ToggleOperator(Operator item) => OperatorCatalogService.Shared.ToggleActive(item);
    public void AddContact()
    {
        if (SelectedSupplier is null) return;
        var contact = new SupplierContact { FirstName = "Nuovo", LastName = "Referente" };
        SelectedSupplier.Contacts.Add(contact); SelectedContact = contact;
    }
    public void DeleteContact()
    {
        if (SelectedSupplier is null || SelectedContact is null) return;
        SelectedSupplier.Contacts.Remove(SelectedContact); SelectedContact = null;
    }
    public void ToggleHistory() { IsHistoryVisible = !IsHistoryVisible; RefreshHistory(); }
    public void SelectPriceEditor(SupplierConfigurationRowViewModel editor) => _selectedPriceEditor = editor;
    public void AddPrice()
    {
        if (SelectedSupplier is null || _selectedPriceEditor?.ValidFrom is null) return;
        _catalog.AddPrice(SelectedSupplier.Id, _selectedPriceEditor.Configuration.ConventionalThickness,
            _selectedPriceEditor.NewPrice, _selectedPriceEditor.ValidFrom.Value);
        _selectedPriceEditor.NewPrice = 0m;
    }
    private void Refresh()
    {
        Configurations.Clear();
        if (SelectedSupplier is not null)
            foreach (var config in SelectedSupplier.ThicknessConfigurations) Configurations.Add(new(config));
        RefreshHistory();
    }
    private void RefreshHistory()
    {
        PriceHistory.Clear();
        if (SelectedSupplier is not null)
            foreach (var price in _catalog.GetHistory(SelectedSupplier.Id)) PriceHistory.Add(price);
    }
}
