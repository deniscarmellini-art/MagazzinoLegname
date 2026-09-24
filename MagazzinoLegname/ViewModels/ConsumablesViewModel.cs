using System.Collections.ObjectModel;
using System.Globalization;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class ConsumablesViewModel : ObservableObject
{
    private readonly ConsumableInventoryService _service;
    private readonly ConsumableOrderService _orders;
    private ConsumableSituationRow? _selectedSituation;
    private ConsumableInventorySnapshot _snapshot = new([], [], []);
    private SaveConsumableInventory? _pendingRequest;
    private string _searchText = string.Empty, _selectedSupplier = "Tutti", _selectedDepartment = "Tutti", _selectedStatus = "Tutti";
    private DateTime _inventoryDate = DateTime.Today;
    private Operator? _selectedOperator;
    private DateTime? _historyFrom, _historyTo;
    private string _historyProduct = "Tutti", _historySupplier = "Tutti", _historyDepartment = "Tutti";
    private bool _isReloading, _isSqlAvailable;
    private string _databaseMessage = "Caricamento inventari SQL...";

    public ConsumablesViewModel() : this(new ConsumableInventoryService(SqlPersistenceRoot.ConsumableInventories)) { }
    public ConsumablesViewModel(ConsumableInventoryService service, ConsumableOrderService? orders = null)
    {
        _service = service;
        _orders = orders ?? new ConsumableOrderService(SqlPersistenceRoot.ConsumableOrders);
    }
    private bool _isOrderEditorVisible;
    public bool IsOrderEditorVisible { get => _isOrderEditorVisible; private set => SetProperty(ref _isOrderEditorVisible, value); }
    public ConsumableSituationRow? SelectedSituation
    {
        get => _selectedSituation;
        set { if (SetProperty(ref _selectedSituation, value)) IsOrderEditorVisible = value is not null; }
    }
    public void OpenOrderEditor() => IsOrderEditorVisible = SelectedSituation is not null;
    public void CancelOrder()
    {
        SelectedSituation?.ResetOrderEditor();
        IsOrderEditorVisible = false;
    }
    public ObservableCollection<ConsumableSqlOrder> OrderHistory { get; } = [];

    public ObservableCollection<ConsumableSituationRow> SituationRows { get; } = [];
    public ObservableCollection<ConsumableInventoryEntryRow> InventoryRows { get; } = [];
    public ObservableCollection<ConsumableHistoryRow> HistoryRows { get; } = [];
    public ObservableCollection<string> Suppliers { get; } = [];
    public ObservableCollection<string> Departments { get; } = [];
    public ObservableCollection<string> Products { get; } = [];
    public ObservableCollection<Operator> Operators { get; } = [];
    public IReadOnlyList<string> Statuses { get; } = ["Tutti", "OK", "Da ordinare", "In ordine", "Sotto scorta · In ordine", "Da verificare"];
    public IReadOnlyList<KeyValuePair<ConsumableOrderStatus, string>> OrderStatuses { get; } =
        Enum.GetValues<ConsumableOrderStatus>().Where(x => x != ConsumableOrderStatus.None)
            .Select(x => new KeyValuePair<ConsumableOrderStatus, string>(x, ConsumableSqlOrder.StatusLabel(x))).ToArray();
    public string DatabaseMessage { get => _databaseMessage; private set => SetProperty(ref _databaseMessage, value); }
    public bool IsSqlAvailable { get => _isSqlAvailable; private set { SetProperty(ref _isSqlAvailable, value); OnPropertyChanged(nameof(CanEditInventory)); } }
    public bool CanEditInventory => IsSqlAvailable && _pendingRequest is null;

    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplySituationFilters(); } }
    public string SelectedSupplier { get => _selectedSupplier; set { if (SetProperty(ref _selectedSupplier, NormalizeFilter(value)) && !_isReloading) ApplySituationFilters(); } }
    public string SelectedDepartment { get => _selectedDepartment; set { if (SetProperty(ref _selectedDepartment, NormalizeFilter(value)) && !_isReloading) ApplySituationFilters(); } }
    public string SelectedStatus { get => _selectedStatus; set { if (SetProperty(ref _selectedStatus, NormalizeFilter(value)) && !_isReloading) ApplySituationFilters(); } }
    public DateTime InventoryDate { get => _inventoryDate; set => SetProperty(ref _inventoryDate, value); }
    public Operator? SelectedOperator { get => _selectedOperator; set => SetProperty(ref _selectedOperator, value); }
    public DateTime? HistoryFrom { get => _historyFrom; set { if (SetProperty(ref _historyFrom, value)) ApplyHistoryFilters(); } }
    public DateTime? HistoryTo { get => _historyTo; set { if (SetProperty(ref _historyTo, value)) ApplyHistoryFilters(); } }
    public string HistoryProduct { get => _historyProduct; set { if (SetProperty(ref _historyProduct, NormalizeFilter(value)) && !_isReloading) ApplyHistoryFilters(); } }
    public string HistorySupplier { get => _historySupplier; set { if (SetProperty(ref _historySupplier, NormalizeFilter(value)) && !_isReloading) ApplyHistoryFilters(); } }
    public string HistoryDepartment { get => _historyDepartment; set { if (SetProperty(ref _historyDepartment, NormalizeFilter(value)) && !_isReloading) ApplyHistoryFilters(); } }

    public int ActiveItems => _snapshot.Items.Count(item => item.IsActive);
    public int BelowMinimum => _snapshot.Items.Count(item => item.IsActive && StatusFor(item) is ConsumableStockStatus.ToOrder or ConsumableStockStatus.BelowMinimumOrdered);
    public int Ordered => _snapshot.Items.Count(item => item.IsActive && _snapshot.HasOpenOrders(item.Id));
    private ConsumableStockStatus StatusFor(ConsumableItem item) => ConsumableOrderRules.Status(item, _snapshot.Latest(item.Id), _snapshot.HasOpenOrders(item.Id));
    public int ToOrder => _snapshot.Items.Count(item => item.IsActive && StatusFor(item) == ConsumableStockStatus.ToOrder);
    public int OkItems => _snapshot.Items.Count(item => item.IsActive && StatusFor(item) == ConsumableStockStatus.Ok);
    public int ToVerify => _snapshot.Items.Count(item => item.IsActive && StatusFor(item) == ConsumableStockStatus.ToVerify);

    public int ConfirmInventory()
    {
        if (!IsSqlAvailable) throw new InvalidOperationException("Ricaricare i dati da SQL prima di confermare l'inventario.");
        if (_pendingRequest is null)
        {
            if (SelectedOperator is null) throw new InvalidOperationException("Selezionare l'operatore.");
            var completed = InventoryRows.Where(row => !string.IsNullOrWhiteSpace(row.NewQuantityText)).ToArray();
            if (completed.Length == 0) throw new InvalidOperationException("Inserire almeno un valore in UDM rilevate.");
            foreach (var row in completed)
                if (!row.NewQuantity.HasValue || !row.CalculatedQuantity.HasValue)
                    throw new InvalidOperationException($"{row.ProductName}: {row.ValidationMessage}");
            _pendingRequest = new SaveConsumableInventory(Guid.NewGuid(), InventoryDate.Date, SelectedOperator.Id,
                completed.Select(row => new ConsumableInventoryInput(row.Item.Id, row.NewQuantity!.Value,
                    row.Item.RowVersion.ToArray(), row.Note)).ToArray());
            OnPropertyChanged(nameof(CanEditInventory));
        }
        var count = _pendingRequest.Readings.Count;
        try { _service.Save(_pendingRequest); }
        catch (ConsumableInventoryConflictException conflict)
        {
            try { Reload(); }
            catch (Exception reloadError) { throw new InvalidOperationException(conflict.Message + " Ricaricamento SQL non riuscito: " + reloadError.Message, conflict); }
            throw new InvalidOperationException(conflict.Message + " Dati ricaricati da SQL: reinserire e verificare i conteggi.", conflict);
        }
        catch (Exception exception)
        {
            // Freeze the original request after an ambiguous failure; retry with the same session ID.
            throw new InvalidOperationException(exception.Message + " È possibile riprovare Conferma inventario con la stessa sessione, oppure aggiornare da SQL.", exception);
        }
        _pendingRequest = null;
        try { Reload(); }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Inventario salvato in SQL, ma ricaricamento non riuscito. Aggiornare da SQL; non ripetere il conteggio.", exception);
        }
        return count;
    }

    public void SaveOrders()
    {
        if (!IsSqlAvailable || SelectedSituation is null) throw new InvalidOperationException("Selezionare un articolo caricato da SQL.");
        if (!SelectedSituation.CanEditOrder) throw new InvalidOperationException("L'ordine selezionato è concluso. Creare un nuovo ordine.");
        try { _orders.Save(SelectedSituation.Order); }
        catch (ConsumableOrderConflictException conflict)
        {
            try { Reload(); }
            catch (Exception reloadError) { throw new InvalidOperationException(conflict.Message + " Ricaricamento SQL non riuscito: " + reloadError.Message, conflict); }
            throw new InvalidOperationException(conflict.Message + " Dati ricaricati da SQL: verificare l'ordine prima di riprovare.", conflict);
        }
        try { Reload(); }
        catch (Exception exception) { throw new InvalidOperationException("Ordine salvato in SQL, ma ricaricamento non riuscito. Aggiornare da SQL prima di altre operazioni.", exception); }
    }

    public void NewOrder()
    {
        if (!IsSqlAvailable || SelectedSituation is null) throw new InvalidOperationException("Selezionare un articolo caricato da SQL.");
        SelectedSituation.CreateNewOrder();
    }

    public void Reload()
    {
        var selectedItemId = SelectedSituation?.Item.Id;
        var operatorId = SelectedOperator?.Id;
        _pendingRequest = null;
        IsSqlAvailable = false;
        _snapshot = new([], [], []);
        SituationRows.Clear(); InventoryRows.Clear(); HistoryRows.Clear(); Operators.Clear(); OrderHistory.Clear();
        SelectedSituation = null;
        SelectedOperator = null;
        RefreshKpis();
        try { _snapshot = _service.Load(); }
        catch (Exception exception) { DatabaseMessage = "Dati SQL non disponibili. " + exception.Message; throw; }
        _isReloading = true;
        var supplier = SelectedSupplier; var department = SelectedDepartment;
        var product = HistoryProduct; var historySupplier = HistorySupplier; var historyDepartment = HistoryDepartment;
        ReplaceOptions(Suppliers, _snapshot.Items.Select(x => x.SupplierName).Concat(_snapshot.Readings.Select(x => x.SupplierSnapshot)));
        ReplaceOptions(Departments, _snapshot.Items.Select(x => x.Department).Concat(_snapshot.Readings.Select(x => x.DepartmentSnapshot)));
        ReplaceOptions(Products, _snapshot.Readings.Select(x => x.ProductSnapshot));
        SelectedSupplier = RestoreFilter(Suppliers, supplier); SelectedDepartment = RestoreFilter(Departments, department);
        HistoryProduct = RestoreFilter(Products, product); HistorySupplier = RestoreFilter(Suppliers, historySupplier);
        HistoryDepartment = RestoreFilter(Departments, historyDepartment);
        foreach (var item in _snapshot.Operators) Operators.Add(item);
        SelectedOperator = Operators.FirstOrDefault(x => x.Id == operatorId) ?? Operators.FirstOrDefault();
        _isReloading = false;
        foreach (var item in _snapshot.Items.Where(x => x.IsActive).OrderBy(x => x.Department).ThenBy(x => x.ProductName))
            InventoryRows.Add(new ConsumableInventoryEntryRow(item, _snapshot.Latest(item.Id)));
        foreach (var order in _snapshot.Orders) OrderHistory.Add(order.Copy());
        ApplySituationFilters(); ApplyHistoryFilters(); RefreshKpis();
        SelectedSituation = SituationRows.FirstOrDefault(x => x.Item.Id == selectedItemId);
        IsSqlAvailable = true;
        DatabaseMessage = "Inventari, ordini e stati aggiornati da SQL. Gli ordini non modificano la giacenza.";
    }

    private void RefreshKpis()
    {
        OnPropertyChanged(nameof(ActiveItems)); OnPropertyChanged(nameof(BelowMinimum)); OnPropertyChanged(nameof(ToOrder));
        OnPropertyChanged(nameof(OkItems)); OnPropertyChanged(nameof(Ordered)); OnPropertyChanged(nameof(ToVerify));
    }

    private void ApplySituationFilters()
    {
        SituationRows.Clear();
        foreach (var item in _snapshot.Items.Where(x => x.IsActive).OrderBy(x => x.ProductName))
        {
            var latest = _snapshot.Latest(item.Id);
            var row = new ConsumableSituationRow(item, latest, _snapshot.Orders.Where(x => x.MaterialId == item.Id).ToArray(),
                _snapshot.OpenOrderTotals.Where(x => x.MaterialId == item.Id).ToArray(), StatusFor(item));
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
        var previousByReading = new Dictionary<Guid, ConsumableSqlReading>();
        foreach (var group in _snapshot.Readings.GroupBy(x => x.ConsumableItemId))
        {
            var readings = group.ToArray();
            for (var index = 0; index + 1 < readings.Length; index++) previousByReading[readings[index].Id] = readings[index + 1];
        }
        foreach (var reading in _snapshot.Readings)
        {
            if (HistoryFrom.HasValue && reading.InventoryDate < HistoryFrom.Value.Date || HistoryTo.HasValue && reading.InventoryDate > HistoryTo.Value.Date) continue;
            if (HistoryProduct != "Tutti" && reading.ProductSnapshot != HistoryProduct || HistorySupplier != "Tutti" && reading.SupplierSnapshot != HistorySupplier || HistoryDepartment != "Tutti" && reading.DepartmentSnapshot != HistoryDepartment) continue;
            HistoryRows.Add(new ConsumableHistoryRow(reading, previousByReading.GetValueOrDefault(reading.Id)));
        }
    }

    private static void ReplaceOptions(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear(); target.Add("Tutti");
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value)) target.Add(value);
    }
    private static string NormalizeFilter(string? value) => string.IsNullOrWhiteSpace(value) ? "Tutti" : value;
    private static string RestoreFilter(IEnumerable<string> options, string previous) => options.Contains(previous, StringComparer.OrdinalIgnoreCase) ? previous : "Tutti";
}

public sealed class ConsumableSituationRow : ObservableObject
{
    private ConsumableSqlOrder? _selectedOrder;
    private ConsumableSqlOrder _order;
    public ConsumableSituationRow(ConsumableItem item, ConsumableSqlReading? latestReading,
        IReadOnlyList<ConsumableSqlOrder> orders, IReadOnlyList<ConsumableOpenOrderTotal> totals, ConsumableStockStatus status)
    {
        Item = item; LatestReading = latestReading; Orders = orders; Totals = totals; Status = status;
        _selectedOrder = orders.FirstOrDefault(x => x.IsOpen) ?? orders.FirstOrDefault();
        _order = _selectedOrder?.Copy() ?? Draft();
    }
    public ConsumableItem Item { get; }
    public ConsumableSqlReading? LatestReading { get; }
    public IReadOnlyList<ConsumableSqlOrder> Orders { get; }
    public IReadOnlyList<ConsumableOpenOrderTotal> Totals { get; }
    public ConsumableStockStatus Status { get; }
    public ConsumableSqlOrder Order { get => _order; private set => SetProperty(ref _order, value); }
    public ConsumableSqlOrder? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (!SetProperty(ref _selectedOrder, value)) return;
            Order = value?.Copy() ?? Draft();
            OnPropertyChanged(nameof(CanEditOrder));
        }
    }
    public bool CanEditOrder => SelectedOrder is null || SelectedOrder.IsOpen;
    public void CreateNewOrder()
    {
        SetProperty(ref _selectedOrder, null, nameof(SelectedOrder));
        Order = Draft();
        OnPropertyChanged(nameof(CanEditOrder));
    }
    public void ResetOrderEditor()
    {
        SetProperty(ref _selectedOrder, Orders.FirstOrDefault(x => x.IsOpen) ?? Orders.FirstOrDefault(), nameof(SelectedOrder));
        Order = _selectedOrder?.Copy() ?? Draft();
        OnPropertyChanged(nameof(CanEditOrder));
    }
    private ConsumableSqlOrder Draft() => new()
    {
        MaterialId = Item.Id, ItemRowVersion = Item.RowVersion.ToArray(), SupplierNameSnapshot = Item.SupplierName,
        ProductNameSnapshot = Item.ProductName, UnitOfMeasureSnapshot = Item.UnitOfMeasure
    };
    public string ProductName => Item.ProductName; public string SupplierName => Item.SupplierName; public string Department => Item.Department;
    public string UnitOfMeasure => LatestReading?.UnitOfMeasureSnapshot ?? Item.UnitOfMeasure;
    public string LatestReadingDisplay => LatestReading?.InventoryDate.ToString("dd/MM/yyyy") ?? "—";
    public string CurrentStockDisplay => LatestReading?.CalculatedQuantity.ToString("0.############") ?? "—";
    public string CountedUnitsDisplay => LatestReading is { } value ? $"{value.CountedUnits:0.######} {value.UnitOfMeasureSnapshot}" : "—";
    public string QuantityPerUnitDisplay => LatestReading?.QuantityPerUnitSnapshot.ToString("0.######") ?? "—";
    public string MinimumStockDisplay => Item.MinimumStock?.ToString("0.######") ?? "—";
    public string ConsumptionDisplay => string.IsNullOrWhiteSpace(Item.ConsumptionAverageText) ? "—" : Item.ConsumptionAverageText;
    public string OrderedDisplay => Totals.Count == 0 ? "—" : string.Join(" + ", Totals.OrderBy(x => x.UnitOfMeasure).Select(x => $"{x.Quantity:0.######} {x.UnitOfMeasure}".Trim()));
    public string StatusDisplay => Status switch
    {
        ConsumableStockStatus.Ok => "OK", ConsumableStockStatus.ToOrder => "Da ordinare",
        ConsumableStockStatus.Ordered => "In ordine", ConsumableStockStatus.BelowMinimumOrdered => "Sotto scorta · In ordine", _ => "Da verificare"
    };
}

public sealed class ConsumableInventoryEntryRow(ConsumableItem item, ConsumableSqlReading? previous) : ObservableObject
{
    private string _newQuantityText = string.Empty, _note = string.Empty;
    public ConsumableItem Item { get; } = item;
    public string ProductName => Item.ProductName; public string Department => Item.Department; public string UnitOfMeasure => Item.UnitOfMeasure;
    public decimal? QuantityPerUnit => Item.QuantityPerUnit;
    public decimal? PreviousQuantity => previous?.CalculatedQuantity;
    public decimal? NewQuantity { get; private set; }
    public decimal? CalculatedQuantity { get; private set; }
    public string ValidationMessage { get; private set; } = string.Empty;
    public string CalculatedQuantityDisplay => CalculatedQuantity?.ToString("0.############") ?? "—";
    public string NewQuantityText
    {
        get => _newQuantityText;
        set
        {
            if (!SetProperty(ref _newQuantityText, value ?? string.Empty)) return;
            NewQuantity = null; CalculatedQuantity = null; ValidationMessage = string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (!decimal.TryParse(value.Trim().Replace('.', ','), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.GetCultureInfo("it-IT"), out var parsed)) ValidationMessage = "UDM rilevate non valide.";
                else
                {
                    try { CalculatedQuantity = ConsumableInventoryRules.Calculate(parsed, QuantityPerUnit ?? 0); NewQuantity = parsed; }
                    catch (Exception exception) when (exception is InvalidOperationException or OverflowException) { ValidationMessage = exception.Message; }
                }
            }
            OnPropertyChanged(nameof(CalculatedQuantity)); OnPropertyChanged(nameof(CalculatedQuantityDisplay));
            OnPropertyChanged(nameof(VariationDisplay)); OnPropertyChanged(nameof(ValidationMessage));
        }
    }
    public string VariationDisplay => CalculatedQuantity.HasValue && PreviousQuantity.HasValue && previous?.UnitOfMeasureSnapshot == UnitOfMeasure
        ? $"{CalculatedQuantity.Value - PreviousQuantity.Value:+0.############;-0.############;0}" : "—";
    public string Note { get => _note; set => SetProperty(ref _note, value); }
}

public sealed record ConsumableHistoryRow(ConsumableSqlReading Reading, ConsumableSqlReading? Previous)
{
    public DateTime Date => Reading.InventoryDate; public string ProductName => Reading.ProductSnapshot; public string SupplierName => Reading.SupplierSnapshot;
    public string Department => Reading.DepartmentSnapshot; public string UnitOfMeasure => Reading.UnitOfMeasureSnapshot;
    public string CountedUnitsDisplay => Reading.CountedUnits.ToString("0.######");
    public string QuantityPerUnitDisplay => Reading.QuantityPerUnitSnapshot.ToString("0.######");
    public string CalculationDisplay => $"{Reading.CountedUnits:0.######} × {Reading.QuantityPerUnitSnapshot:0.######} = {Reading.CalculatedQuantity:0.############}";
    public decimal? StockVariation => Previous is null || Previous.UnitOfMeasureSnapshot != UnitOfMeasure ? null : Reading.CalculatedQuantity - Previous.CalculatedQuantity;
    public string StockVariationDisplay => StockVariation.HasValue ? $"{StockVariation:+0.############;-0.############;0}" : "—";
    public string Operator => Reading.OperatorSnapshot; public string Note => Reading.Note;
}
