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
    private bool _refreshing;
    private bool _forecastAvailable;
    private DateTime _selectedWeekA;
    private DateTime _selectedWeekB;

    public PlanningViewModel()
    {
        var today = DateTime.Today;
        _selectedWeekA = StartOfWeek(today);
        _selectedWeekB = _selectedWeekA.AddDays(7);
        BuildCalendar();
        BuildForecast();
        _suppliers.CatalogChanged += (_, _) => { if (!_refreshing) BuildCalendar(); };
        _planning.PlanningChanged += (_, _) => Refresh();
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
        [("SETTIMANA A", _selectedWeekA), ("SETTIMANA B", _selectedWeekB)];

    private void BuildCalendar()
    {
        PlanningDiagnostics.Log($"9 BuildCalendar START\n{new System.Diagnostics.StackTrace(true)}");
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
        PlanningDiagnostics.Log("9 BuildCalendar END");
    }

    private void BuildForecast()
    {
        PlanningDiagnostics.Log("10 Forecast rebuild START");
        ForecastRows.Clear();
        foreach (var material in Materials)
        {
            var cells = ActiveWeeks.Select(week => new PlanningForecastWeekViewModel(
                _planning.GetOrCreateConsumption(week.Monday, material.Thickness, material.Quality),
                _planning, ReportError, _forecastAvailable));
            ForecastRows.Add(new PlanningForecastRowViewModel(
                material.Thickness, material.Quality, cells));
        }
        RecalculateForecast();
        PlanningDiagnostics.Log("10 Forecast rebuild END");
    }

    private void RecalculateForecast()
    {
        if (!_forecastAvailable) return;
        var packages = _inventory.BuildInventory().Where(x => x.IsPresent && x.IsAccountedPackage).ToArray();
        foreach (var row in ForecastRows)
        {
            var opening = packages.Where(x => x.ConventionalThickness == row.ConventionalThickness && x.Quality == row.Quality)
                .Sum(x => x.InventoryCubicMeters);
            var forecast = PlanningForecastCalculator.Calculate(opening, row.ConventionalThickness, row.Quality,
                _selectedWeekA, _selectedWeekB, _planning.Arrivals, _planning.Consumptions);
            foreach (var week in row.Weeks) week.Update(forecast.Single(x => x.WeekStart == week.Consumption.WeekStart));
        }
    }

    private void RefreshActiveWeeks()
    {
        PlanningDiagnostics.Log("11 SelectedPeriod refresh");
        Refresh();
        OnPropertyChanged(nameof(PeriodText));
    }

    private string _databaseMessage = "Pianificazione: aggiornare da SQL.";
    public string DatabaseMessage { get => _databaseMessage; private set => SetProperty(ref _databaseMessage, value); }
    public event Action<string>? ErrorOccurred;
    private void ReportError(Exception error)
    {
        System.Diagnostics.Debug.WriteLine($"[Pianificazione UI] {error}");
        DatabaseMessage = error.Message; ErrorOccurred?.Invoke(error.Message);
    }
    public void Refresh()
    {
        if (_refreshing) return;
        _refreshing = true;
        _forecastAvailable = false;
        try
        {
            _planning.Invalidate();
            _settings.Reload();
            _suppliers.Reload();
            ClassificationWorkflowService.Shared.ReloadInboundLoads();
            _inventory.ReloadSqlTerminalMovements();
            PlanningDiagnostics.Log("8 InventoryProjection reload OK");
            _planning.Reload();
            _forecastAvailable = true;
            BuildCalendar(); BuildForecast();
            DatabaseMessage = "Giacenza, arrivi e fabbisogni aggiornati da SQL. M³ salvati all’uscita dal campo; vuoto/0 elimina.";
        }
        catch (Exception error)
        {
            _forecastAvailable = false;
            _planning.Invalidate();
            // Do not rebuild the same failing tree again inside the error handler.
            CalendarWeeks.Clear(); ForecastRows.Clear();
            PlanningDiagnostics.Log($"Planning refresh failed: {error}");
            ReportError(_planning.ArrivalCommitCompleted
                ? new InvalidOperationException("Il salvataggio è riuscito ma non è stato possibile aggiornare la pagina. Premere Aggiorna da SQL.", error)
                : error);
        }
        finally { _refreshing = false; }
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
    public PlannedArrival Arrival { get; private set; }
    private bool _pending;
    private bool _retired;
    public IReadOnlyList<string> Options => Arrival.Status == PlannedArrivalStatus.Confirmed ? [$"✓ Arrivato · {Arrival.ConventionalThickness:0} {Arrival.Quality}"] : !_supplierActive ? ["Nessun arrivo", Selection] : AvailableOptions;
    public bool IsEditable => !_pending && !_retired && _planning.IsAvailable && Arrival.Status == PlannedArrivalStatus.Expected && (_supplierActive || Arrival.RowVersion.Length > 0);
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
        if (_pending || _retired) return;
        _pending = true;
        // Keep the binding's source coherent until the current Selector transaction ends.
        // SQL remains authoritative: the draft is replaced by the repository reload.
        var previous = Arrival;
        Arrival = draft.Copy();
        if (delete) Arrival.LoadQuantity = 0;
        void Commit()
        {
            PlanningDiagnostics.Log($"Arrival cell Persist delete={delete} Id={draft.Id}");
            try
            {
                if (delete) _planning.Delete(draft); else _planning.Save(draft);
                _retired = true; // Reload has replaced this cell, including its RowVersion.
            }
            catch (Exception error)
            {
                Arrival = previous;
                PlanningDiagnostics.Log($"Arrival workflow failed: {error}");
                _report(error);
                OnPropertyChanged(nameof(Selection));
                OnPropertyChanged(nameof(LoadQuantity));
            }
            finally { _pending = false; }
        }
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) Commit();
        else dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind, (Action)Commit);

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

public sealed class PlanningForecastWeekViewModel(PlannedConsumption consumption, PlanningDataService planning,
    Action<Exception> report, bool available) : ObservableObject
{
    private PlanningWeeklyForecast? _forecast;
    public PlannedConsumption Consumption { get; } = consumption;
    public bool IsEditable => available && planning.IsAvailable;
    public string QuantityText
    {
        get => Consumption.ExpectedCubicMeters == 0 ? "" : Consumption.ExpectedCubicMeters.ToString("0.#########", System.Globalization.CultureInfo.CurrentCulture);
        set
        {
            if (!IsEditable) return;
            try
            {
                decimal quantity = 0;
                if (!string.IsNullOrWhiteSpace(value) && !decimal.TryParse(value,
                    System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowLeadingWhite | System.Globalization.NumberStyles.AllowTrailingWhite,
                    System.Globalization.CultureInfo.CurrentCulture, out quantity))
                    throw new InvalidOperationException("Inserire un numero valido di m³ (esempio: 10,50). Vuoto o zero elimina il fabbisogno.");
                if (quantity < 0) throw new InvalidOperationException("Il fabbisogno non può essere negativo.");
                if (quantity == Consumption.ExpectedCubicMeters) return;
                var draft = Consumption.Copy(); draft.ExpectedCubicMeters = quantity;
                planning.Save(draft);
            }
            catch (Exception error) { report(error); }
            OnPropertyChanged();
        }
    }
    public decimal OpeningBalance => _forecast?.OpeningBalance ?? 0;
    public decimal ExpectedArrivals => _forecast?.ExpectedArrivals ?? 0;
    public decimal ClosingBalance => _forecast?.ClosingBalance ?? 0;
    public string OpeningDisplay => _forecast is null ? "—" : OpeningBalance.ToString("N2");
    public string ArrivalsDisplay => _forecast is null ? "—" : ExpectedArrivals.ToString("N2");
    public string ClosingDisplay => _forecast is null ? "—" : ClosingBalance.ToString("N2");
    public bool IsNegative => _forecast is not null && ClosingBalance < 0;
    public void Update(PlanningWeeklyForecast forecast)
    {
        _forecast = forecast;
        OnPropertyChanged(nameof(OpeningBalance)); OnPropertyChanged(nameof(ExpectedArrivals));
        OnPropertyChanged(nameof(ClosingBalance)); OnPropertyChanged(nameof(OpeningDisplay));
        OnPropertyChanged(nameof(ArrivalsDisplay)); OnPropertyChanged(nameof(ClosingDisplay)); OnPropertyChanged(nameof(IsNegative));
    }
}
