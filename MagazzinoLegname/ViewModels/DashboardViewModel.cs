using System.Linq;
using System.Collections.ObjectModel;
using System;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly InventoryProjectionService _projection = InventoryProjectionService.Shared;
    private IReadOnlyList<InventoryPackage> _allPackages = [];

    public DashboardViewModel()
    {
        ClassificationWorkflowService.Shared.WorkflowChanged += (_, _) => Reload();
        _projection.InventoryChanged += (_, _) => Reload();
        Reload();
    }

    public int PresentPackages { get; private set; }
    public decimal InventoryCubicMeters { get; private set; }
    public decimal CubicMetersToConsolidate { get; private set; }
    public decimal RealCubicMeters { get; private set; }
    public decimal InventoryValue { get; private set; }
    public int LoadsToClassify { get; private set; }
    public int GroupsToClassify { get; private set; }
    public int GroupsToConsolidate { get; private set; }

    public ObservableCollection<ThicknessRow> ThicknessRows { get; } = new ObservableCollection<ThicknessRow>();

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
    public string InventoryValueDisplay => InventoryValue.ToString("N2") + " €";

    private void Reload()
    {
        _allPackages = _projection.BuildInventory();
        PresentPackages = _allPackages.Count;
        InventoryCubicMeters = _allPackages.Sum(p => p.InventoryCubicMeters);
        CubicMetersToConsolidate = _allPackages.Where(p => !p.UsesRealCubicMeters).Sum(p => p.InventoryCubicMeters);
        RealCubicMeters = _allPackages.Where(p => p.UsesRealCubicMeters).Sum(p => p.InventoryCubicMeters);
        InventoryValue = _allPackages.Sum(p => p.PackageValue);
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

        // Calcola giacenza per spessore (raggruppando per ConventionalThickness arrotondato)
        var groups = _allPackages
            .GroupBy(p => (int)decimal.Round(p.ConventionalThickness, 0, MidpointRounding.AwayFromZero))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Assicurare che i spessori principali siano presenti
        int[] required = new[] { 23, 34, 44 };
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

        OnPropertyChanged(nameof(PresentPackages)); OnPropertyChanged(nameof(InventoryCubicMeters));
        OnPropertyChanged(nameof(CubicMetersToConsolidate)); OnPropertyChanged(nameof(RealCubicMeters));
        OnPropertyChanged(nameof(InventoryValue));
        OnPropertyChanged(nameof(LoadsToClassify)); OnPropertyChanged(nameof(GroupsToClassify));
        OnPropertyChanged(nameof(GroupsToConsolidate));

        OnPropertyChanged(nameof(PresentPackagesDisplay)); OnPropertyChanged(nameof(InventoryCubicMetersDisplay));
        OnPropertyChanged(nameof(CubicMetersToConsolidateDisplay)); OnPropertyChanged(nameof(RealCubicMetersDisplay));
        OnPropertyChanged(nameof(InventoryValueDisplay));
    }
}
