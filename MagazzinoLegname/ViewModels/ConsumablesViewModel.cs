using System.Collections.ObjectModel;
using System.Globalization;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class ConsumablesViewModel : ObservableObject
{
    private readonly ConsumablesStore _store = ConsumablesStore.Shared;
    private string _searchText = string.Empty, _selectedSupplier = "Tutti", _selectedDepartment = "Tutti", _selectedStatus = "Tutti";
    private DateTime _inventoryDate = DateTime.Today;
    private string _selectedOperator = string.Empty;
    private DateTime? _historyFrom = DateTime.Today.AddYears(-1), _historyTo = DateTime.Today;
    private string _historyProduct = "Tutti", _historySupplier = "Tutti", _historyDepartment = "Tutti";

    public ConsumablesViewModel()
    {
        Operators = OperatorCatalogService.Shared.ActiveOperatorNames;
        _selectedOperator = Operators.FirstOrDefault() ?? string.Empty;
        _store.Changed += (_, _) => Reload();
        Reload();
    }

    public ObservableCollection<ConsumableSituationRow> SituationRows { get; } = [];
    public ObservableCollection<ConsumableInventoryEntryRow> InventoryRows { get; } = [];
    public ObservableCollection<ConsumableHistoryRow> HistoryRows { get; } = [];
    public ObservableCollection<string> Suppliers { get; } = [];
    public ObservableCollection<string> Departments { get; } = [];
    public ObservableCollection<string> Products { get; } = [];
    public ReadOnlyObservableCollection<string> Operators { get; }
    public IReadOnlyList<string> Statuses { get; } = ["Tutti", "OK", "Da ordinare", "Sotto scorta · in ordine", "In ordine", "Da verificare"];
    public Array OrderStatuses => Enum.GetValues<ConsumableOrderStatus>();

    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplySituationFilters(); } }
    public string SelectedSupplier { get => _selectedSupplier; set { if (SetProperty(ref _selectedSupplier, value)) ApplySituationFilters(); } }
    public string SelectedDepartment { get => _selectedDepartment; set { if (SetProperty(ref _selectedDepartment, value)) ApplySituationFilters(); } }
    public string SelectedStatus { get => _selectedStatus; set { if (SetProperty(ref _selectedStatus, value)) ApplySituationFilters(); } }
    public DateTime InventoryDate { get => _inventoryDate; set => SetProperty(ref _inventoryDate, value); }
    public string SelectedOperator { get => _selectedOperator; set => SetProperty(ref _selectedOperator, value); }
    public DateTime? HistoryFrom { get => _historyFrom; set { if (SetProperty(ref _historyFrom, value)) ApplyHistoryFilters(); } }
    public DateTime? HistoryTo { get => _historyTo; set { if (SetProperty(ref _historyTo, value)) ApplyHistoryFilters(); } }
    public string HistoryProduct { get => _historyProduct; set { if (SetProperty(ref _historyProduct, value)) ApplyHistoryFilters(); } }
    public string HistorySupplier { get => _historySupplier; set { if (SetProperty(ref _historySupplier, value)) ApplyHistoryFilters(); } }
    public string HistoryDepartment { get => _historyDepartment; set { if (SetProperty(ref _historyDepartment, value)) ApplyHistoryFilters(); } }

    public int ActiveItems => _store.Items.Count(item => item.IsActive);
    public int BelowMinimum => _store.Items.Count(item => item.IsActive && _store.StatusFor(item) is ConsumableStockStatus.ToOrder or ConsumableStockStatus.BelowMinimumOrdered);
    public int ToOrder => _store.ItemsToOrder;
    public int Ordered => _store.Items.Count(item => item.IsActive && _store.OrderFor(item.Id).IsOpen);
    public int ToVerify => _store.Items.Count(item => item.IsActive && _store.StatusFor(item) == ConsumableStockStatus.ToVerify);

    public int ConfirmInventory()
    {
        if (string.IsNullOrWhiteSpace(SelectedOperator)) throw new InvalidOperationException("Selezionare l'operatore.");
        var completed = InventoryRows.Where(item => item.NewQuantity.HasValue).ToArray();
        if (completed.Length == 0) throw new InvalidOperationException("Inserire almeno una nuova giacenza.");
        if (completed.Any(item => !item.Item.QuantityPerUnit.HasValue || item.Item.QuantityPerUnit <= 0))
            throw new InvalidOperationException("Impostare una Qtà per UDM valida per tutti gli articoli rilevati.");
        _store.AddReadings(completed.Select(item => new ConsumableInventoryReading
        {
            MaterialId = item.Item.Id, ReadingDate = InventoryDate, CountedUnits = item.NewQuantity!.Value,
            QuantityPerUnitSnapshot = item.Item.QuantityPerUnit!.Value,
            Quantity = item.NewQuantity.Value * item.Item.QuantityPerUnit.Value,
            Operator = SelectedOperator, Note = item.Note.Trim(), Origin = "Inventario"
        }));
        return completed.Length;
    }

    public void SaveOrders() => _store.NotifyChanged();

    public void Reload()
    {
        ReplaceOptions(Suppliers, "Tutti", _store.Items.Select(item => item.SupplierName));
        ReplaceOptions(Departments, "Tutti", _store.Items.Select(item => item.Department));
        ReplaceOptions(Products, "Tutti", _store.Items.Select(item => item.ProductName));
        ApplySituationFilters();
        InventoryRows.Clear();
        foreach (var item in _store.Items.Where(item => item.IsActive).OrderBy(item => item.ProductName))
            InventoryRows.Add(new ConsumableInventoryEntryRow(item, _store.LatestReading(item.Id)));
        ApplyHistoryFilters();
        OnPropertyChanged(nameof(ActiveItems)); OnPropertyChanged(nameof(BelowMinimum)); OnPropertyChanged(nameof(ToOrder));
        OnPropertyChanged(nameof(Ordered)); OnPropertyChanged(nameof(ToVerify));
    }

    private void ApplySituationFilters()
    {
        SituationRows.Clear();
        foreach (var item in _store.Items.Where(item => item.IsActive).OrderBy(item => item.ProductName))
        {
            var row = new ConsumableSituationRow(item, _store.LatestReading(item.Id), _store.OrderFor(item.Id), _store.StatusFor(item));
            if (!string.IsNullOrWhiteSpace(SearchText) && !item.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !item.InternalCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) continue;
            if (SelectedSupplier != "Tutti" && item.SupplierName != SelectedSupplier) continue;
            if (SelectedDepartment != "Tutti" && item.Department != SelectedDepartment) continue;
            if (SelectedStatus != "Tutti" && row.StatusDisplay != SelectedStatus) continue;
            SituationRows.Add(row);
        }
    }

    private void ApplyHistoryFilters()
    {
        HistoryRows.Clear();
        foreach (var reading in _store.Readings.OrderByDescending(item => item.ReadingDate))
        {
            var item = _store.Items.FirstOrDefault(candidate => candidate.Id == reading.MaterialId);
            if (item is null || HistoryFrom.HasValue && reading.ReadingDate.Date < HistoryFrom.Value.Date || HistoryTo.HasValue && reading.ReadingDate.Date > HistoryTo.Value.Date) continue;
            if (HistoryProduct != "Tutti" && item.ProductName != HistoryProduct || HistorySupplier != "Tutti" && item.SupplierName != HistorySupplier || HistoryDepartment != "Tutti" && item.Department != HistoryDepartment) continue;
            var previous = _store.PreviousReading(item.Id, reading);
            HistoryRows.Add(new ConsumableHistoryRow(item, reading, previous));
        }
    }

    private static void ReplaceOptions(ObservableCollection<string> target, string all, IEnumerable<string> values)
    {
        target.Clear(); target.Add(all);
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value)) target.Add(value);
    }
}

public sealed record ConsumableSituationRow(ConsumableItem Item, ConsumableInventoryReading? LatestReading,
    ConsumableOrderInfo Order, ConsumableStockStatus Status)
{
    public string ProductName => Item.ProductName; public string SupplierName => Item.SupplierName; public string Department => Item.Department;
    public string UnitOfMeasure => Item.UnitOfMeasure; public string? PhotoPath => Item.PhotoPath;
    public string LatestReadingDisplay => LatestReading is null ? "—" : LatestReading.ReadingDate.ToString("dd/MM/yyyy");
    public string CurrentStockDisplay => LatestReading is null ? "—" : LatestReading.Quantity.ToString("N2");
    public string CountedUnitsDisplay => LatestReading?.CountedUnits is { } value ? $"{value:N2} {Item.UnitOfMeasure}" : "—";
    public string QuantityPerUnitDisplay => Item.QuantityPerUnit?.ToString("N2") ?? "—";
    public string MinimumStockDisplay => Item.MinimumStock?.ToString("N2") ?? "—";
    public string ConsumptionDisplay => string.IsNullOrWhiteSpace(Item.ConsumptionAverageText) ? "—" : Item.ConsumptionAverageText;
    public string LeadTimeDisplay => Item.LeadTimeDays.HasValue ? $"{Item.LeadTimeDays} gg" : "—";
    public string OrderedDisplay => Order.IsOpen ? $"{Order.Quantity:N2} {Item.UnitOfMeasure}" : "—";
    public string StatusDisplay => Status switch { ConsumableStockStatus.Ok => "OK", ConsumableStockStatus.ToOrder => "Da ordinare", ConsumableStockStatus.BelowMinimumOrdered => "Sotto scorta · in ordine", ConsumableStockStatus.Ordered => "In ordine", _ => "Da verificare" };
}

public sealed class ConsumableInventoryEntryRow(ConsumableItem item, ConsumableInventoryReading? previous) : ObservableObject
{
    private string _newQuantityText = string.Empty, _note = string.Empty;
    public ConsumableItem Item { get; } = item; public string ProductName => Item.ProductName; public string Department => Item.Department; public string UnitOfMeasure => Item.UnitOfMeasure;
    public decimal? QuantityPerUnit => Item.QuantityPerUnit;
    public decimal? PreviousQuantity => previous?.Quantity; public DateTime? PreviousDate => previous?.ReadingDate;
    public decimal? NewQuantity { get; private set; }
    public decimal? CalculatedQuantity => NewQuantity.HasValue && QuantityPerUnit is > 0 ? NewQuantity.Value * QuantityPerUnit.Value : null;
    public string CalculatedQuantityDisplay => CalculatedQuantity?.ToString("N2") ?? "—";
    public string NewQuantityText { get => _newQuantityText; set { var normalized = (value ?? "").Replace('.', ','); if (!SetProperty(ref _newQuantityText, normalized)) return; NewQuantity = decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out var parsed) && parsed >= 0 ? parsed : null; OnPropertyChanged(nameof(CalculatedQuantity)); OnPropertyChanged(nameof(CalculatedQuantityDisplay)); OnPropertyChanged(nameof(VariationDisplay)); } }
    public string VariationDisplay => CalculatedQuantity.HasValue ? $"{CalculatedQuantity.Value - (PreviousQuantity ?? 0m):+0.##;-0.##;0}" : "—";
    public string Note { get => _note; set => SetProperty(ref _note, value); }
}

public sealed record ConsumableHistoryRow(ConsumableItem Item, ConsumableInventoryReading Reading, ConsumableInventoryReading? Previous)
{
    public DateTime Date => Reading.ReadingDate; public string ProductName => Item.ProductName; public string SupplierName => Item.SupplierName;
    public string Department => Item.Department; public decimal Quantity => Reading.Quantity; public string UnitOfMeasure => Item.UnitOfMeasure;
    public string CountedUnitsDisplay => Reading.CountedUnits?.ToString("N2") ?? "Legacy";
    public string QuantityPerUnitDisplay => Reading.QuantityPerUnitSnapshot?.ToString("N2") ?? "—";
    public string CalculationDisplay => Reading.CountedUnits.HasValue && Reading.QuantityPerUnitSnapshot.HasValue ? $"{Reading.CountedUnits:N2} × {Reading.QuantityPerUnitSnapshot:N2} = {Reading.Quantity:N2}" : Reading.Quantity.ToString("N2");
    public decimal? StockVariation => Previous is null ? null : Reading.Quantity - Previous.Quantity;
    public string StockVariationDisplay => StockVariation.HasValue ? $"{StockVariation:+0.##;-0.##;0}" : "—";
    public string Operator => Reading.Operator ?? "Importazione legacy"; public string Note => Reading.Note;
}
