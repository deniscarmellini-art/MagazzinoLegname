using System.Linq;
using System.Collections.ObjectModel;
using System;
using System.Windows.Input;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly InventoryProjectionService _projection = InventoryProjectionService.Shared;
    private readonly PlanningDataService _planning = PlanningDataService.Shared;
    private readonly PlanningSettingsService _planningSettings = PlanningSettingsService.Shared;
    private readonly SupplierCatalogService _suppliers = SupplierCatalogService.Shared;
    private readonly MaterialParameters _materialParameters = MaterialParametersService.Shared.Parameters;
    private IReadOnlyList<InventoryPackage> _allPackages = [];

    public DashboardViewModel()
    {
        ClassificationWorkflowService.Shared.WorkflowChanged += (_, _) => Reload();
        _projection.InventoryChanged += (_, _) => Reload();
        _planning.PlanningChanged += (_, _) => Reload();
        _planningSettings.SettingsChanged += (_, _) => Reload();
        _suppliers.CatalogChanged += (_, _) => Reload();
        Reload();
    }

    public int PresentPackages { get; private set; }
    public decimal InventoryCubicMeters { get; private set; }
    public decimal CubicMetersToConsolidate { get; private set; }
    public decimal RealCubicMeters { get; private set; }
    public decimal InventoryValue { get; private set; }
    public int PackagesWithoutPrice { get; private set; }
    public int LoadsToClassify { get; private set; }
    public int GroupsToClassify { get; private set; }
    public int GroupsToConsolidate { get; private set; }

    public ObservableCollection<ThicknessRow> ThicknessRows { get; } = new ObservableCollection<ThicknessRow>();
    public ObservableCollection<HistoryMovementRow> RecentMovements { get; } = [];
    public ObservableCollection<WeeklyPlannedArrivalRow> WeeklyPlannedArrivals { get; } = [];
    public int WeeklyPlannedLoadCount { get; private set; }
    public decimal WeeklyPlannedCubicMeters { get; private set; }
    public bool IsWeeklyPlanningEmpty => WeeklyPlannedArrivals.Count == 0;
    public string WeeklyPlannedLoadCountDisplay => $"{WeeklyPlannedLoadCount} {(WeeklyPlannedLoadCount == 1 ? "carico previsto" : "carichi previsti")}";
    public string WeeklyPlannedCubicMetersDisplay => $"{WeeklyPlannedCubicMeters:N2} m³ teorici";

    public sealed class ThicknessRow
    {
        public ThicknessRow(int thickness,
            decimal classifiedC, decimal toClassifyC,
            decimal classifiedVista, decimal toClassifyVista)
        {
            Thickness = thickness;
            ClassifiedC = classifiedC;
            ToClassifyC = toClassifyC;
            ClassifiedVista = classifiedVista;
            ToClassifyVista = toClassifyVista;
            TotalC = ClassifiedC + ToClassifyC;
            TotalVista = ClassifiedVista + ToClassifyVista;
            TotalAll = TotalC + TotalVista;
        }
        public int Thickness { get; }
        public decimal ClassifiedC { get; }
        public decimal ToClassifyC { get; }
        public decimal ClassifiedVista { get; }
        public decimal ToClassifyVista { get; }
        public decimal TotalC { get; }
        public decimal TotalVista { get; }
        public decimal TotalAll { get; }

        public string ThicknessDisplay => Thickness.ToString();
        public string ClassifiedCDisplay => ClassifiedC.ToString("N2");
        public string ToClassifyCDisplay => ToClassifyC.ToString("N2");
        public string ClassifiedVistaDisplay => ClassifiedVista.ToString("N2");
        public string ToClassifyVistaDisplay => ToClassifyVista.ToString("N2");
        public string TotalCDisplay => TotalC.ToString("N2");
        public string TotalVistaDisplay => TotalVista.ToString("N2");
        public string TotalAllDisplay => TotalAll.ToString("N2");
    }

    // Formatted displays
    public string PresentPackagesDisplay => PresentPackages.ToString();
    public string InventoryCubicMetersDisplay => InventoryCubicMeters.ToString("N2");
    public string CubicMetersToConsolidateDisplay => CubicMetersToConsolidate.ToString("N2");
    public string RealCubicMetersDisplay => RealCubicMeters.ToString("N2");
    public string InventoryValueDisplay => _allPackages.Count == 0 || PackagesWithoutPrice == _allPackages.Count ? "N/D"
        : InventoryValue.ToString("N0") + " €" + (PackagesWithoutPrice > 0 ? " · PARZIALE" : "");

    private void Reload()
    {
        _allPackages = _projection.BuildInventory();
        PresentPackages = _allPackages.Count;
        InventoryCubicMeters = _allPackages.Sum(p => p.InventoryCubicMeters);
        CubicMetersToConsolidate = _allPackages.Where(p => !p.UsesRealCubicMeters).Sum(p => p.InventoryCubicMeters);
        RealCubicMeters = _allPackages.Where(p => p.UsesRealCubicMeters).Sum(p => p.InventoryCubicMeters);
        InventoryValue = _allPackages.Sum(p => p.PackageValue ?? 0m);
        PackagesWithoutPrice = _allPackages.Count(p => !p.AppliedPrice.HasValue);
        LoadsToClassify = _allPackages
            .Where(p => p.ClassificationStatus == "Da classificare")
            .Select(p => p.LoadId)
            .Distinct()
            .Count();
        GroupsToClassify = _allPackages
            .Where(p => p.ClassificationStatus == "Da classificare")
            .Select(p => p.MaterialGroupId)
            .Distinct()
            .Count();
        GroupsToConsolidate = _allPackages
            .Where(p => p.ClassificationStatus != "Da classificare" && !p.UsesRealCubicMeters)
            .Select(p => p.MaterialGroupId)
            .Distinct()
            .Count();

        // Raggruppa tramite la famiglia centralizzata, senza alterare lo spessore ingresso.
        var groups = _allPackages
            .Select(package => new { Package = package, Family = _materialParameters.FindFamily(package.IncomingThickness)?.ConventionalThickness })
            .Where(item => item.Family.HasValue)
            .GroupBy(item => (int)item.Family!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Package).ToList());

        var required = _materialParameters.ThicknessFamilies.Select(family => (int)family.ConventionalThickness).Distinct().ToArray();
        foreach (var r in required) if (!groups.ContainsKey(r)) groups[r] = new List<InventoryPackage>();

        ThicknessRows.Clear();
        foreach (var key in groups.Keys.OrderBy(k => k))
        {
            var list = groups[key];
            // Qualità C
            var listC = list.Where(p => string.Equals(p.Quality, "C", StringComparison.OrdinalIgnoreCase));
            decimal classifiedC = listC.Where(p => p.ClassificationStatus != "Da classificare").Sum(p => p.InventoryCubicMeters);
            decimal toClassifyC = listC.Where(p => p.ClassificationStatus == "Da classificare").Sum(p => p.InventoryCubicMeters);
            // Qualità VISTA
            var listV = list.Where(p => string.Equals(p.Quality, "VISTA", StringComparison.OrdinalIgnoreCase));
            decimal classifiedV = listV.Where(p => p.ClassificationStatus != "Da classificare").Sum(p => p.InventoryCubicMeters);
            decimal toClassifyV = listV.Where(p => p.ClassificationStatus == "Da classificare").Sum(p => p.InventoryCubicMeters);

            var row = new ThicknessRow(key, classifiedC, toClassifyC, classifiedV, toClassifyV);
            ThicknessRows.Add(row);
        }

        RecentMovements.Clear();
        foreach (var movement in HistoryViewModel.BuildMovementRows()
                     .OrderByDescending(item => item.DateTime)
                     .Take(6))
            RecentMovements.Add(movement);

        var today = DateTime.Today;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).Date;
        var friday = monday.AddDays(4);
        var arrivals = _planning.Arrivals
            .Where(item => item.Date.Date >= monday && item.Date.Date <= friday
                && item.Status == PlannedArrivalStatus.Expected
                && item.LoadQuantity > 0 && item.ConventionalThickness.HasValue
                && !string.IsNullOrWhiteSpace(item.Quality))
            .OrderBy(item => item.Date)
            .ThenBy(item => _suppliers.Suppliers.FirstOrDefault(supplier => supplier.Id == item.SupplierId)?.Name)
            .ToList();

        WeeklyPlannedArrivals.Clear();
        foreach (var arrival in arrivals)
        {
            var supplierName = _suppliers.Suppliers
                .FirstOrDefault(item => item.Id == arrival.SupplierId)?.Name ?? "—";
            WeeklyPlannedArrivals.Add(new WeeklyPlannedArrivalRow(arrival.Date, supplierName,
                arrival.ConventionalThickness!.Value, arrival.Quality!, arrival.LoadQuantity,
                new RelayCommand(() => _planning.ConfirmArrival(arrival.Id))));
        }
        WeeklyPlannedLoadCount = arrivals.Sum(item => item.LoadQuantity);
        WeeklyPlannedCubicMeters = arrivals.Sum(item => item.LoadQuantity
            * _planningSettings.Settings.GetStandardCubicMetersPerExpectedLoad(item.ConventionalThickness!.Value));

        OnPropertyChanged(nameof(PresentPackages)); OnPropertyChanged(nameof(InventoryCubicMeters));
        OnPropertyChanged(nameof(CubicMetersToConsolidate)); OnPropertyChanged(nameof(RealCubicMeters));
        OnPropertyChanged(nameof(InventoryValue));
        OnPropertyChanged(nameof(PackagesWithoutPrice));
        OnPropertyChanged(nameof(LoadsToClassify)); OnPropertyChanged(nameof(GroupsToClassify));
        OnPropertyChanged(nameof(GroupsToConsolidate));

        OnPropertyChanged(nameof(PresentPackagesDisplay)); OnPropertyChanged(nameof(InventoryCubicMetersDisplay));
        OnPropertyChanged(nameof(CubicMetersToConsolidateDisplay)); OnPropertyChanged(nameof(RealCubicMetersDisplay));
        OnPropertyChanged(nameof(InventoryValueDisplay));
        OnPropertyChanged(nameof(WeeklyPlannedLoadCount));
        OnPropertyChanged(nameof(WeeklyPlannedCubicMeters));
        OnPropertyChanged(nameof(IsWeeklyPlanningEmpty));
        OnPropertyChanged(nameof(WeeklyPlannedLoadCountDisplay));
        OnPropertyChanged(nameof(WeeklyPlannedCubicMetersDisplay));
    }
}

public sealed record WeeklyPlannedArrivalRow(DateTime Date, string SupplierName,
    decimal ConventionalThickness, string Quality, int LoadQuantity, ICommand ConfirmArrivalCommand)
{
    public string DateDisplay
    {
        get
        {
            var value = Date.ToString("ddd dd/MM");
            return char.ToUpper(value[0]) + value[1..];
        }
    }

    public string MaterialDisplay => $"{ConventionalThickness:0} {Quality}";
}
