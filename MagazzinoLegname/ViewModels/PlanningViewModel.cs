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
    private readonly DateTime _firstMonday;

    public PlanningViewModel()
    {
        var today = DateTime.Today;
        _firstMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).Date;
        BuildCalendar();
        BuildForecast();
        _suppliers.CatalogChanged += (_, _) => { BuildCalendar(); RecalculateForecast(); };
        _planning.PlanningChanged += (_, _) => RecalculateForecast();
        _settings.SettingsChanged += (_, _) => RecalculateForecast();
        _inventory.InventoryChanged += (_, _) => RecalculateForecast();
        ClassificationWorkflowService.Shared.WorkflowChanged += (_, _) => RecalculateForecast();
    }

    public ObservableCollection<string> ActiveSupplierNames { get; } = [];
    public ObservableCollection<PlanningCalendarWeekViewModel> CalendarWeeks { get; } = [];
    public ObservableCollection<PlanningForecastRowViewModel> ForecastRows { get; } = [];
    public PlanningSettings Settings => _settings.Settings;
    public string PeriodText => $"Dal {_firstMonday:dd/MM/yyyy} al {_firstMonday.AddDays(18):dd/MM/yyyy}";

    private void BuildCalendar()
    {
        var activeSuppliers = _suppliers.Suppliers.Where(item => item.IsActive).ToList();
        ActiveSupplierNames.Clear();
        foreach (var supplier in activeSuppliers) ActiveSupplierNames.Add(supplier.Name);

        CalendarWeeks.Clear();
        for (var weekIndex = 0; weekIndex < 3; weekIndex++)
        {
            var weekStart = _firstMonday.AddDays(weekIndex * 7);
            var days = Enumerable.Range(0, 5)
                .Select(offset => new PlanningDayViewModel(weekStart.AddDays(offset))).ToList();
            var supplierRows = activeSuppliers.Select(supplier =>
                new PlanningSupplierWeekRowViewModel(supplier.Name,
                    days.Select(day => new PlanningArrivalCellViewModel(
                        _planning.GetOrCreateArrival(supplier.Id, day.Date))))).ToList();
            CalendarWeeks.Add(new PlanningCalendarWeekViewModel(
                $"SETTIMANA {weekIndex + 1}", weekStart, days, supplierRows));
        }
        OnPropertyChanged(nameof(PeriodText));
    }

    private void BuildForecast()
    {
        ForecastRows.Clear();
        foreach (var material in Materials)
        {
            var cells = Enumerable.Range(0, 3).Select(weekIndex =>
            {
                var weekStart = _firstMonday.AddDays(weekIndex * 7);
                return new PlanningForecastWeekViewModel(
                    _planning.GetOrCreateConsumption(weekStart, material.Thickness, material.Quality));
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
                .Where(package => package.ConventionalThickness == row.ConventionalThickness
                    && package.Quality == row.Quality)
                .Sum(package => package.InventoryCubicMeters);

            for (var weekIndex = 0; weekIndex < row.Weeks.Count; weekIndex++)
            {
                var weekStart = _firstMonday.AddDays(weekIndex * 7);
                var weekEnd = weekStart.AddDays(4);
                var loadCount = _planning.Arrivals.Where(arrival => arrival.Date.Date >= weekStart
                        && arrival.Date.Date <= weekEnd
                        && arrival.ConventionalThickness == row.ConventionalThickness
                        && arrival.Quality == row.Quality)
                    .Sum(arrival => arrival.LoadQuantity);
                var standardCubicMeters = Settings.GetStandardCubicMetersPerExpectedLoad(row.ConventionalThickness);
                var arrivals = loadCount * standardCubicMeters;
                var cell = row.Weeks[weekIndex];
                cell.Update(openingBalance, loadCount, arrivals);
                openingBalance = cell.ClosingBalance;
            }
        }
    }
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
    public static IReadOnlyList<string> AvailableOptions { get; } =
    ["Nessun arrivo", "23 C", "23 VISTA", "34 C", "34 VISTA", "44 C", "44 VISTA"];

    public PlanningArrivalCellViewModel(PlannedArrival arrival) => Arrival = arrival;
    public PlannedArrival Arrival { get; }
    public IReadOnlyList<string> Options => AvailableOptions;
    public string Selection
    {
        get => Arrival.LoadQuantity <= 0 || !Arrival.ConventionalThickness.HasValue
            ? "Nessun arrivo"
            : $"{Arrival.ConventionalThickness.Value:0} {Arrival.Quality}";
        set
        {
            if (value == Selection) return;
            if (value == "Nessun arrivo")
            {
                Arrival.LoadQuantity = 0;
                Arrival.ConventionalThickness = null;
                Arrival.Quality = null;
            }
            else
            {
                var parts = value.Split(' ', 2);
                Arrival.ConventionalThickness = decimal.Parse(parts[0]);
                Arrival.Quality = parts[1];
                Arrival.LoadQuantity = 1;
            }
            OnPropertyChanged();
        }
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
