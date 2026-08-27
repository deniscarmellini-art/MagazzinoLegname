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
    private string? _legacyFilePath;
    private LegacyImportReport? _legacyReport;
    private bool _isLegacyAnalysisRunning;
    private string? _legacyAnalysisError;
    private LegacyInitialInventoryImportPlan? _legacyImportPlan;
    private LegacyInitialInventoryImportResult? _legacyImportResult;
    private LegacyClosedHistoryImportPlan? _legacyClosedHistoryPlan;
    private LegacyClosedHistoryImportResult? _legacyClosedHistoryResult;

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
    public string SelectedSection { get => _selectedSection; set { if (SetProperty(ref _selectedSection, value)) { OnPropertyChanged(nameof(IsSuppliersSection)); OnPropertyChanged(nameof(IsMaterialParametersSection)); OnPropertyChanged(nameof(IsPlanningParametersSection)); OnPropertyChanged(nameof(IsOperatorsSection)); OnPropertyChanged(nameof(IsLegacyImportSection)); } } }
    public bool IsSuppliersSection => SelectedSection == "Suppliers";
    public bool IsMaterialParametersSection => SelectedSection == "MaterialParameters";
    public bool IsPlanningParametersSection => SelectedSection == "PlanningParameters";
    public bool IsOperatorsSection => SelectedSection == "Operators";
    public bool IsLegacyImportSection => SelectedSection == "LegacyImport";
    public string? LegacyFilePath { get => _legacyFilePath; set { if (SetProperty(ref _legacyFilePath, value)) OnPropertyChanged(nameof(CanAnalyzeLegacy)); } }
    public LegacyImportReport? LegacyReport { get => _legacyReport; private set { if (SetProperty(ref _legacyReport, value)) OnPropertyChanged(nameof(HasLegacyReport)); } }
    public bool HasLegacyReport => LegacyReport is not null;
    public bool IsLegacyAnalysisRunning { get => _isLegacyAnalysisRunning; private set { if (SetProperty(ref _isLegacyAnalysisRunning, value)) OnPropertyChanged(nameof(CanAnalyzeLegacy)); } }
    public bool CanAnalyzeLegacy => !IsLegacyAnalysisRunning && !string.IsNullOrWhiteSpace(LegacyFilePath);
    public string? LegacyAnalysisError { get => _legacyAnalysisError; private set => SetProperty(ref _legacyAnalysisError, value); }
    public LegacyInitialInventoryImportPlan? LegacyImportPlan { get => _legacyImportPlan; private set { if (SetProperty(ref _legacyImportPlan, value)) { OnPropertyChanged(nameof(HasLegacyImportPlan)); OnPropertyChanged(nameof(CanImportLegacy)); } } }
    public LegacyInitialInventoryImportResult? LegacyImportResult { get => _legacyImportResult; private set => SetProperty(ref _legacyImportResult, value); }
    public bool HasLegacyImportPlan => LegacyImportPlan is not null;
    public bool CanImportLegacy => LegacyImportPlan?.CanCommit == true && LegacyImportResult is null;
    public LegacyClosedHistoryImportPlan? LegacyClosedHistoryPlan { get => _legacyClosedHistoryPlan; private set { if (SetProperty(ref _legacyClosedHistoryPlan, value)) { OnPropertyChanged(nameof(HasLegacyClosedHistoryPlan)); OnPropertyChanged(nameof(CanImportClosedHistory)); } } }
    public LegacyClosedHistoryImportResult? LegacyClosedHistoryResult { get => _legacyClosedHistoryResult; private set => SetProperty(ref _legacyClosedHistoryResult, value); }
    public bool HasLegacyClosedHistoryPlan => LegacyClosedHistoryPlan is not null;
    public bool CanImportClosedHistory => LegacyClosedHistoryPlan?.CanCommit == true && LegacyClosedHistoryResult is null;
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
    public void ShowLegacyImport() { IsHistoryVisible = false; SelectedSection = "LegacyImport"; }
    public async Task AnalyzeLegacyAsync()
    {
        if (!CanAnalyzeLegacy) return;
        IsLegacyAnalysisRunning = true; LegacyAnalysisError = null; LegacyReport = null; LegacyImportPlan = null; LegacyImportResult = null; LegacyClosedHistoryPlan = null; LegacyClosedHistoryResult = null;
        try
        {
            var path = LegacyFilePath!;
            LegacyReport = await Task.Run(() => new LegacyImportAnalyzer().Analyze(new LegacyExcelReader().Read(path)));
            try { LegacyImportPlan = LegacyInitialInventoryImportService.Shared.BuildPlan(LegacyReport); }
            catch (InvalidOperationException exception) { LegacyAnalysisError = $"Analisi completata, importazione non abilitata: {exception.Message}"; }
            LegacyClosedHistoryPlan = LegacyHistoricalStore.Shared.BuildPlan(LegacyReport);
        }
        catch (Exception exception) { LegacyAnalysisError = exception.Message; }
        finally { IsLegacyAnalysisRunning = false; }
    }
    public LegacyInitialInventoryImportResult ImportLegacyInMemory(string? operatorName)
    {
        if (!CanImportLegacy || LegacyImportPlan is null) throw new InvalidOperationException("Il piano di importazione non è pronto o presenta collisioni.");
        LegacyImportResult = LegacyInitialInventoryImportService.Shared.Commit(LegacyImportPlan, operatorName);
        OnPropertyChanged(nameof(CanImportLegacy));
        return LegacyImportResult;
    }
    public LegacyClosedHistoryImportResult ImportClosedHistoryInMemory()
    {
        if (!CanImportClosedHistory || LegacyClosedHistoryPlan is null) throw new InvalidOperationException("Il piano dello storico chiuso non è pronto o presenta collisioni.");
        LegacyClosedHistoryResult = LegacyHistoricalStore.Shared.Commit(LegacyClosedHistoryPlan);
        OnPropertyChanged(nameof(CanImportClosedHistory));
        return LegacyClosedHistoryResult;
    }
    public void ResetOperationalTestData()
    {
        InMemoryTestDataResetService.Shared.ResetOperationalData();
        LegacyReport = null; LegacyImportPlan = null; LegacyImportResult = null; LegacyClosedHistoryPlan = null; LegacyClosedHistoryResult = null; LegacyAnalysisError = null;
    }
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
