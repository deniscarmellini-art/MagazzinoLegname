using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class PlanningViewModel : ObservableObject
{
    private static readonly (decimal Thickness, string Quality)[] Materials =
    [
        (23m, "C"), (23m, "VISTA"), (34m, "C"),
        (34m, "VISTA"), (44m, "C"), (44m, "VISTA")
    ];

    private readonly SupplierCatalogService _suppliers = SupplierCatalogService.Shared;
    private readonly PlanningDataService _planning = PlanningDataService.Shared;
    private readonly PlanningSettingsService _settings = PlanningSettingsService.Shared;
    private readonly InventoryProjectionService _inventory = InventoryProjectionService.Shared;
    private readonly MaterialParameters _materialParameters = MaterialParametersService.Shared.Parameters;
    private DateTime _selectedWeekA;
    private DateTime _selectedWeekB;

    public PlanningViewModel()
    {
        var today = DateTime.Today;
        _selectedWeekA = StartOfWeek(today);
        _selectedWeekB = _selectedWeekA.AddDays(7);
        BuildCalendar();
        BuildForecast();
        _suppliers.CatalogChanged += (_, _) => { BuildCalendar(); RecalculateForecast(); };
        _planning.PlanningChanged += (_, _) => { BuildCalendar(); RecalculateForecast(); };
        _settings.SettingsChanged += (_, _) => RecalculateForecast();
        _inventory.InventoryChanged += (_, _) => RecalculateForecast();
        ClassificationWorkflowService.Shared.WorkflowChanged += (_, _) => RecalculateForecast();
    }

    public ObservableCollection<string> ActiveSupplierNames { get; } = [];
    public ObservableCollection<PlanningCalendarWeekViewModel> CalendarWeeks { get; } = [];
    public ObservableCollection<PlanningForecastRowViewModel> ForecastRows { get; } = [];
    public PlanningSettings Settings => _settings.Settings;
    public DateTime SelectedWeekA
    {
        get => _selectedWeekA;
        set
        {
            var monday = StartOfWeek(value);
            if (!SetProperty(ref _selectedWeekA, monday)) return;
            if (_selectedWeekB <= monday)
            {
                _selectedWeekB = monday.AddDays(7);
                OnPropertyChanged(nameof(SelectedWeekB));
            }
            RefreshActiveWeeks();
        }
    }
    public DateTime SelectedWeekB
    {
        get => _selectedWeekB;
        set
        {
            var monday = StartOfWeek(value);
            if (monday <= _selectedWeekA) monday = _selectedWeekA.AddDays(7);
            if (!SetProperty(ref _selectedWeekB, monday)) return;
            RefreshActiveWeeks();
        }
    }
    public string PeriodText => $"A: {_selectedWeekA:dd/MM}–{_selectedWeekA.AddDays(4):dd/MM}  ·  B: {_selectedWeekB:dd/MM}–{_selectedWeekB.AddDays(4):dd/MM}";
    private IReadOnlyList<(string Label, DateTime Monday)> ActiveWeeks =>
        [("SETTIMANA A", _selectedWeekA), ("SETTIMANA B", _selectedWeekB), ("SETTIMANA C", _selectedWeekB.AddDays(7))];

    private void BuildCalendar()
    {
        var activeSuppliers = _suppliers.Suppliers.Where(item => item.IsActive || _planning.Arrivals.Any(x => x.SupplierId == item.Id)).ToList();
        ActiveSupplierNames.Clear();
        foreach (var supplier in activeSuppliers) ActiveSupplierNames.Add(supplier.Name + (supplier.IsActive ? "" : " (inattivo)"));

        CalendarWeeks.Clear();
        foreach (var activeWeek in ActiveWeeks)
        {
            var weekStart = activeWeek.Monday;
            var days = Enumerable.Range(0, 5)
                .Select(offset => new PlanningDayViewModel(weekStart.AddDays(offset))).ToList();
            var supplierRows = activeSuppliers.Select(supplier =>
                new PlanningSupplierWeekRowViewModel(supplier.Name + (supplier.IsActive ? "" : " (inattivo)"),
                    days.Select(day => new PlanningArrivalCellViewModel(
                        _planning.GetOrCreateArrival(supplier.Id, day.Date), _planning, supplier.IsActive, ReportError)))).ToList();
            CalendarWeeks.Add(new PlanningCalendarWeekViewModel(
                activeWeek.Label, weekStart, days, supplierRows));
        }
        OnPropertyChanged(nameof(PeriodText));
    }

    private void BuildForecast()
    {
        ForecastRows.Clear();
        foreach (var material in Materials)
        {
            var cells = ActiveWeeks.Select(activeWeek =>
            {
                return new PlanningForecastWeekViewModel(
                    _planning.GetOrCreateConsumption(activeWeek.Monday, material.Thickness, material.Quality));
            });
            ForecastRows.Add(new PlanningForecastRowViewModel(
                material.Thickness, material.Quality, cells));
        }
        RecalculateForecast();
    }

    private void RecalculateForecast()
    {
        var packages = _inventory.BuildInventory();
        foreach (var row in ForecastRows)
        {
            var openingBalance = packages
                .Where(package => (_materialParameters.FindFamily(package.IncomingThickness)?.ConventionalThickness ?? 0m) == row.ConventionalThickness
                    && package.Quality == row.Quality)
                .Sum(package => package.InventoryCubicMeters);

            for (var weekIndex = 0; weekIndex < row.Weeks.Count; weekIndex++)
            {
                var weekStart = ActiveWeeks[weekIndex].Monday;
                var weekEnd = weekStart.AddDays(4);
                var loadCount = _planning.Arrivals.Where(arrival => arrival.Date.Date >= weekStart
                        && arrival.Date.Date <= weekEnd
                        && arrival.Status == PlannedArrivalStatus.Expected
                        && arrival.ConventionalThickness == row.ConventionalThickness
                        && arrival.Quality == row.Quality)
                    .Sum(arrival => arrival.LoadQuantity);
                var arrivals = _planning.Arrivals.Where(x => x.Date.Date >= weekStart && x.Date.Date <= weekEnd && x.Status == PlannedArrivalStatus.Expected
                    && x.ConventionalThickness == row.ConventionalThickness && x.Quality == row.Quality).Sum(x => x.ExpectedCubicMeters);
                var cell = row.Weeks[weekIndex];
                cell.Update(openingBalance, loadCount, arrivals);
                openingBalance = cell.ClosingBalance;
            }
        }
    }

    private void RefreshActiveWeeks()
    {
        Refresh();
        BuildForecast();
        OnPropertyChanged(nameof(PeriodText));
    }

    private string _databaseMessage = "Carichi pianificati: aggiornare da SQL.";
    public string DatabaseMessage { get => _databaseMessage; private set => SetProperty(ref _databaseMessage, value); }
    public event Action<string>? ErrorOccurred;
    private void ReportError(Exception error)
    {
        System.Diagnostics.Debug.WriteLine($"[Pianificazione UI] {error}");
        DatabaseMessage = error.Message; ErrorOccurred?.Invoke(error.Message);
    }
    public void Refresh()
    {
        try { _planning.Invalidate(); _settings.Reload(); _suppliers.Reload(); _planning.Reload(); BuildCalendar(); RecalculateForecast(); DatabaseMessage = "Carichi pianificati aggiornati da SQL. Le modifiche alle celle vengono salvate automaticamente."; }
        catch (Exception error) { ReportError(error); }
    }
    private static DateTime StartOfWeek(DateTime date) =>
        date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}

public sealed record PlanningDayViewModel(DateTime Date)
{
    public string Header => $"{Date:ddd dd/MM}";
}

public sealed class PlanningCalendarWeekViewModel(
    string label, DateTime weekStart, IEnumerable<PlanningDayViewModel> days,
    IEnumerable<PlanningSupplierWeekRowViewModel> supplierRows)
{
    public string Label { get; } = label;
    public DateTime WeekStart { get; } = weekStart;
    public ObservableCollection<PlanningDayViewModel> Days { get; } = new(days);
    public ObservableCollection<PlanningSupplierWeekRowViewModel> SupplierRows { get; } = new(supplierRows);
}

public sealed class PlanningSupplierWeekRowViewModel(
    string supplierName, IEnumerable<PlanningArrivalCellViewModel> cells)
{
    public string SupplierName { get; } = supplierName;
    public ObservableCollection<PlanningArrivalCellViewModel> Cells { get; } = new(cells);
}

public sealed class PlanningArrivalCellViewModel : ObservableObject
{
    public static IReadOnlyList<string> AvailableOptions { get; } = ["Nessun arrivo", "23 C", "23 VISTA", "34 C", "34 VISTA", "44 C", "44 VISTA"];
    private readonly PlanningDataService _planning;
    private readonly Action<Exception> _report;
    private readonly bool _supplierActive;
    public PlanningArrivalCellViewModel(PlannedArrival arrival, PlanningDataService planning, bool supplierActive, Action<Exception> report)
    { Arrival = arrival; _planning = planning; _supplierActive = supplierActive; _report = report; }
    public PlannedArrival Arrival { get; }
    public IReadOnlyList<string> Options => Arrival.Status == PlannedArrivalStatus.Confirmed ? [$"✓ Arrivato · {Arrival.ConventionalThickness:0} {Arrival.Quality}"] : !_supplierActive ? ["Nessun arrivo", Selection] : AvailableOptions;
    public bool IsEditable => _planning.IsAvailable && Arrival.Status == PlannedArrivalStatus.Expected && (_supplierActive || Arrival.RowVersion.Length > 0);
    public bool CanChangeQuantity => IsEditable && _supplierActive && Arrival.LoadQuantity > 0;
    public string Detail => $"{Arrival.Date:dd/MM/yyyy} · {Arrival.LoadQuantity} carichi · {Arrival.ExpectedCubicMeters:N2} m³ · {Arrival.Notes}";
    public int LoadQuantity
    {
        get => Arrival.LoadQuantity;
        set
        {
            if (!CanChangeQuantity || value == Arrival.LoadQuantity) return;
            if (value <= 0) { _report(new InvalidOperationException("Inserire almeno un carico; per eliminare scegliere Nessun arrivo.")); OnPropertyChanged(); return; }
            var draft = Arrival.Copy(); draft.LoadQuantity = value; Persist(draft, false);
        }
    }
    public string Selection
    {
        get => Arrival.Status == PlannedArrivalStatus.Confirmed ? $"✓ Arrivato · {Arrival.ConventionalThickness:0} {Arrival.Quality}" :
            Arrival.LoadQuantity <= 0 ? "Nessun arrivo" : $"{Arrival.ConventionalThickness:0} {Arrival.Quality}";
        set
        {
            if (!IsEditable || string.IsNullOrEmpty(value) || value == Selection) return;
            if (value == "Nessun arrivo") { Persist(Arrival.Copy(), true); return; }
            if (!_supplierActive) { _report(new InvalidOperationException("Fornitore inattivo: è possibile solo eliminare la pianificazione esistente.")); OnPropertyChanged(); return; }
            if (!AvailableOptions.Contains(value)) return;
            var parts = value.Split(' ', 2); var draft = Arrival.Copy();
            draft.ConventionalThickness = decimal.Parse(parts[0]); draft.Quality = parts[1]; draft.LoadQuantity = Math.Max(1, draft.LoadQuantity);
            Persist(draft, false);
        }
    }
    private void Persist(PlannedArrival draft, bool delete)
    {
        try { if (delete) _planning.Delete(draft); else _planning.Save(draft); }
        catch (Exception error) { _report(error); }
        OnPropertyChanged(nameof(Selection)); OnPropertyChanged(nameof(LoadQuantity));
    }
}

public sealed class PlanningForecastRowViewModel(
    decimal conventionalThickness, string quality,
    IEnumerable<PlanningForecastWeekViewModel> weeks)
{
    public decimal ConventionalThickness { get; } = conventionalThickness;
    public string Quality { get; } = quality;
    public string MaterialLabel => $"{ConventionalThickness:0} {Quality}";
    public ObservableCollection<PlanningForecastWeekViewModel> Weeks { get; } = new(weeks);
}

public sealed class PlanningForecastWeekViewModel : ObservableObject
{
    private decimal _openingBalance;
    private int _expectedLoadCount;
    private decimal _expectedArrivals;
    private decimal _closingBalance;

    public PlanningForecastWeekViewModel(PlannedConsumption consumption) => Consumption = consumption;
    public PlannedConsumption Consumption { get; }
    public decimal OpeningBalance { get => _openingBalance; private set => SetProperty(ref _openingBalance, value); }
    public int ExpectedLoadCount { get => _expectedLoadCount; private set => SetProperty(ref _expectedLoadCount, value); }
    public decimal ExpectedArrivals { get => _expectedArrivals; private set => SetProperty(ref _expectedArrivals, value); }
    public decimal ClosingBalance
    {
        get => _closingBalance;
        private set
        {
            if (SetProperty(ref _closingBalance, value)) OnPropertyChanged(nameof(IsNegative));
        }
    }
    public bool IsNegative => ClosingBalance < 0m;

    public void Update(decimal openingBalance, int expectedLoadCount, decimal expectedArrivals)
    {
        OpeningBalance = openingBalance;
        ExpectedLoadCount = expectedLoadCount;
        ExpectedArrivals = expectedArrivals;
        ClosingBalance = OpeningBalance + ExpectedArrivals - Consumption.CubicMeters;
    }
}
