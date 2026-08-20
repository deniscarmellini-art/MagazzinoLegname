using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;
using SkiaSharp;

namespace MagazzinoLegname.ViewModels;

public sealed class StatisticsViewModel : ObservableObject
{
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;
    private readonly InventoryProjectionService _inventory = InventoryProjectionService.Shared;
    private readonly SupplierCatalogService _suppliers = SupplierCatalogService.Shared;
    private string _selectedPeriod = "Questo mese";
    private bool _includeInactiveSuppliers;
    private string _selectedSupplier = "Tutti";
    private string _selectedThickness = "Tutti";
    private string _selectedQuality = "Tutte";
    private DateTime? _dateFrom;
    private DateTime? _dateTo;

    public StatisticsViewModel()
    {
        Periods = ["Questo mese", "Ultimi 3 mesi", "Ultimi 6 mesi", "Anno corrente", "Personalizzato"];
        Thicknesses = ["Tutti", "23", "34", "44"];
        Qualities = ["Tutte", "C", "VISTA"];
        _workflow.WorkflowChanged += (_, _) => Refresh();
        _inventory.InventoryChanged += (_, _) => Refresh();
        _suppliers.CatalogChanged += (_, _) => { RefreshSuppliers(); Refresh(); };
        RefreshSuppliers();
        UpdateFilterDates();
        Refresh();
    }

    public IReadOnlyList<string> Periods { get; }
    public IReadOnlyList<string> Thicknesses { get; }
    public IReadOnlyList<string> Qualities { get; }
    public ObservableCollection<string> Suppliers { get; } = [];
    public ObservableCollection<SupplierStatisticsRow> SupplierRows { get; } = [];
    public ObservableCollection<QualityWasteMatrixRow> QualityWasteRows { get; } = [];
    public ObservableCollection<ConsumptionStatisticsRow> ConsumptionRows { get; } = [];
    public ObservableCollection<StatisticsTimePoint> TimePoints { get; } = [];
    public ISeries[] TimeChartSeries { get; private set; } = [];
    public Axis[] TimeChartXAxes { get; private set; } = [];
    public Axis[] TimeChartYAxes { get; private set; } = [];
    public string TimeChartGranularity { get; private set; } = string.Empty;
    public ObservableCollection<SupplierQualityWastePoint> SupplierQualityWastePoints { get; } = [];
    public ISeries[] SupplierQualityWasteChartSeries { get; private set; } = [];
    public Axis[] SupplierQualityWasteChartXAxes { get; private set; } = [];
    public Axis[] SupplierQualityWasteChartYAxes { get; private set; } = [];
    public ObservableCollection<SupplierPurchasePoint> SupplierPurchasePoints { get; } = [];
    public ISeries[] SupplierPurchaseChartSeries { get; private set; } = [];
    public Axis[] SupplierPurchaseChartXAxes { get; private set; } = [];
    public Axis[] SupplierPurchaseChartYAxes { get; private set; } = [];

    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (!SetProperty(ref _selectedPeriod, value)) return;
            UpdateFilterDates();
            OnPropertyChanged(nameof(IsCustomPeriod));
            Refresh();
        }
    }
    public bool IncludeInactiveSuppliers
    {
        get => _includeInactiveSuppliers;
        set
        {
            if (!SetProperty(ref _includeInactiveSuppliers, value)) return;
            RefreshSuppliers();
            Refresh();
        }
    }
    public string SelectedSupplier { get => _selectedSupplier; set { if (SetProperty(ref _selectedSupplier, value)) Refresh(); } }
    public string SelectedThickness { get => _selectedThickness; set { if (SetProperty(ref _selectedThickness, value)) Refresh(); } }
    public string SelectedQuality { get => _selectedQuality; set { if (SetProperty(ref _selectedQuality, value)) Refresh(); } }
    public DateTime? DateFrom { get => _dateFrom; set { if (SetProperty(ref _dateFrom, value) && IsCustomPeriod) Refresh(); } }
    public DateTime? DateTo { get => _dateTo; set { if (SetProperty(ref _dateTo, value) && IsCustomPeriod) Refresh(); } }
    public bool IsCustomPeriod => SelectedPeriod == "Personalizzato";

    public decimal CubicMetersEntered { get; private set; }
    public decimal RealCubicMetersAfterClassification { get; private set; }
    public decimal CubicMetersDischarged { get; private set; }
    public decimal AverageQualityWastePercentage { get; private set; }
    public decimal AverageProcessingWastePercentage { get; private set; }
    public decimal PurchaseValue { get; private set; }
    public string CubicMetersEnteredDisplay => CubicMetersEntered.ToString("N2");
    public string RealCubicMetersAfterClassificationDisplay => RealCubicMetersAfterClassification.ToString("N2");
    public string CubicMetersDischargedDisplay => CubicMetersDischarged.ToString("N2");
    public string AverageQualityWasteDisplay => $"{AverageQualityWastePercentage:N2}%";
    public string AverageProcessingWasteDisplay => $"{AverageProcessingWastePercentage:N2}%";
    public string PurchaseValueDisplay => $"{PurchaseValue:N2} €";

    private void RefreshSuppliers()
    {
        Suppliers.Clear();
        Suppliers.Add("Tutti");
        foreach (var supplier in ScopedSuppliers().OrderBy(item => item.Name)) Suppliers.Add(supplier.Name);
        if (Suppliers.Contains(SelectedSupplier)) return;

        _selectedSupplier = "Tutti";
        OnPropertyChanged(nameof(SelectedSupplier));
    }

    private IEnumerable<Supplier> ScopedSuppliers() => _suppliers.Suppliers
        .Where(item => IncludeInactiveSuppliers || item.IsActive);

    private bool IsSupplierInScope(string supplierName) => IncludeInactiveSuppliers
        || _suppliers.Suppliers.Any(item => item.IsActive
            && string.Equals(item.Name, supplierName, StringComparison.OrdinalIgnoreCase));

    private void UpdateFilterDates()
    {
        var today = DateTime.Today;
        (_dateFrom, _dateTo) = SelectedPeriod switch
        {
            "Ultimi 3 mesi" => (today.AddMonths(-3), today),
            "Ultimi 6 mesi" => (today.AddMonths(-6), today),
            "Anno corrente" => (new DateTime(today.Year, 1, 1), today),
            "Personalizzato" => (_dateFrom ?? today.AddMonths(-1), _dateTo ?? today),
            _ => (new DateTime(today.Year, today.Month, 1), today)
        };
        OnPropertyChanged(nameof(DateFrom));
        OnPropertyChanged(nameof(DateTo));
    }

    private void Refresh()
    {
        var from = (DateFrom ?? DateTime.MinValue).Date;
        var to = (DateTo ?? DateTime.MaxValue).Date.AddDays(1).AddTicks(-1);
        if (from > to) (from, to) = (to.Date, from.Date.AddDays(1).AddTicks(-1));

        var allGroups = _workflow.Loads
            .SelectMany(load => load.Groups.Select(group => new GroupContext(load, group)))
            .Where(MatchesDimensions)
            .ToList();
        var entryGroups = allGroups.Where(item => item.Load.ArrivalDate >= from && item.Load.ArrivalDate <= to).ToList();

        var groupLookup = _workflow.Loads
            .SelectMany(load => load.Groups.Select(group => new GroupContext(load, group)))
            .ToDictionary(item => item.Group.GroupId);
        var adjustments = _workflow.WasteAdjustmentHistory
            .Where(item => groupLookup.TryGetValue(item.MaterialGroupId, out var context)
                && context.Load.ArrivalDate >= from && context.Load.ArrivalDate <= to
                && MatchesDimensions(context))
            .Select(item => new AdjustmentContext(item, groupLookup[item.MaterialGroupId]))
            .ToList();
        var discharges = _inventory.DischargeMovements
            .Where(item => item.DischargeDate >= from && item.DischargeDate <= to)
            .Where(item => groupLookup.TryGetValue(item.MaterialGroupId, out var context) && MatchesDimensions(context))
            .Select(item => new DischargeContext(item.DischargedCubicMeters, item.DischargeDate, groupLookup[item.MaterialGroupId]))
            .ToList();
        var supplierQualityWastePoints = BuildSupplierQualityWastePoints(adjustments);

        RefreshKpis(entryGroups, adjustments, discharges);
        RefreshSupplierAnalysis(entryGroups, supplierQualityWastePoints);
        RefreshQualityWaste(adjustments);
        RefreshConsumption(discharges, groupLookup, from, to);
        RefreshChartSeries(entryGroups, discharges, from, to);
        RefreshSupplierQualityWasteChart(supplierQualityWastePoints);
        RefreshSupplierPurchaseChart(BuildSupplierPurchasePoints());
    }

    private void RefreshKpis(IReadOnlyCollection<GroupContext> entries,
        IReadOnlyCollection<AdjustmentContext> adjustments, IReadOnlyCollection<DischargeContext> discharges)
    {
        CubicMetersEntered = entries.Sum(item => item.Group.IncomingPhysicalCubicMeters);
        RealCubicMetersAfterClassification = adjustments.Sum(item => item.Adjustment.RealAvailableCubicMeters);
        CubicMetersDischarged = discharges.Sum(item => item.CubicMeters);
        PurchaseValue = entries.Sum(item => item.Group.LineValue);
        AverageProcessingWastePercentage = WeightedPercentage(entries.Sum(item => item.Group.IncomingPhysicalCubicMeters),
            entries.Sum(item => item.Group.IncomingPhysicalCubicMeters - item.Group.TheoreticalUsefulCubicMeters));
        AverageQualityWastePercentage = WeightedPercentage(adjustments.Sum(item => item.Adjustment.TheoreticalUsefulCubicMeters),
            adjustments.Sum(item => item.Adjustment.TheoreticalUsefulCubicMeters - item.Adjustment.RealAvailableCubicMeters));

        OnPropertyChanged(nameof(CubicMetersEnteredDisplay));
        OnPropertyChanged(nameof(RealCubicMetersAfterClassificationDisplay));
        OnPropertyChanged(nameof(CubicMetersDischargedDisplay));
        OnPropertyChanged(nameof(AverageQualityWasteDisplay));
        OnPropertyChanged(nameof(AverageProcessingWasteDisplay));
        OnPropertyChanged(nameof(PurchaseValueDisplay));
    }

    private void RefreshSupplierAnalysis(IReadOnlyCollection<GroupContext> entries,
        IReadOnlyCollection<SupplierQualityWastePoint> qualityWastePoints)
    {
        SupplierRows.Clear();
        var qualityWasteBySupplier = qualityWastePoints.ToDictionary(item => item.SupplierName);
        foreach (var supplierGroup in entries.GroupBy(item => item.Load.SupplierName).OrderBy(item => item.Key))
        {
            var cubicMeters = supplierGroup.Sum(item => item.Group.IncomingPhysicalCubicMeters);
            var value = supplierGroup.Sum(item => item.Group.LineValue);
            SupplierRows.Add(new SupplierStatisticsRow(supplierGroup.Key, cubicMeters, value,
                cubicMeters == 0m ? 0m : value / cubicMeters,
                qualityWasteBySupplier.TryGetValue(supplierGroup.Key, out var qualityWaste)
                    ? qualityWaste.QualityWastePercentage : 0m,
                WeightedPercentage(cubicMeters,
                    supplierGroup.Sum(item => item.Group.IncomingPhysicalCubicMeters - item.Group.TheoreticalUsefulCubicMeters))));
        }
    }

    private static List<SupplierQualityWastePoint> BuildSupplierQualityWastePoints(
        IReadOnlyCollection<AdjustmentContext> adjustments) => adjustments
        .Where(item => item.Adjustment.TheoreticalUsefulCubicMeters > 0m)
        .GroupBy(item => item.Context.Load.SupplierName)
        .Select(group =>
        {
            var theoretical = group.Sum(item => item.Adjustment.TheoreticalUsefulCubicMeters);
            var real = group.Sum(item => item.Adjustment.RealAvailableCubicMeters);
            return new SupplierQualityWastePoint(group.Key, theoretical, real,
                WeightedPercentage(theoretical, theoretical - real));
        })
        .OrderBy(item => item.QualityWastePercentage)
        .ThenBy(item => item.SupplierName)
        .ToList();

    private List<SupplierPurchasePoint> BuildSupplierPurchasePoints() => SupplierRows
        .Where(item => item.PurchasedCubicMeters > 0m)
        .Select(item => new SupplierPurchasePoint(item.SupplierName, item.PurchasedCubicMeters,
            item.Value, item.WeightedAveragePrice))
        .OrderByDescending(item => item.IncomingCubicMeters)
        .ThenBy(item => item.SupplierName)
        .ToList();

    private void RefreshQualityWaste(IReadOnlyCollection<AdjustmentContext> adjustments)
    {
        QualityWasteRows.Clear();
        IEnumerable<string> supplierNames = SelectedSupplier == "Tutti"
            ? ScopedSuppliers().Select(item => item.Name).OrderBy(item => item)
            : new[] { SelectedSupplier };
        foreach (var supplierName in supplierNames)
        {
            string Cell(decimal thickness, string quality)
            {
                var rows = adjustments.Where(item => item.Context.Load.SupplierName == supplierName
                    && item.Context.Group.ConventionalThickness == thickness && item.Context.Group.Quality == quality).ToList();
                if (rows.Count == 0) return "—";
                return $"{WeightedPercentage(rows.Sum(item => item.Adjustment.TheoreticalUsefulCubicMeters), rows.Sum(item => item.Adjustment.TheoreticalUsefulCubicMeters - item.Adjustment.RealAvailableCubicMeters)):N2}%";
            }
            QualityWasteRows.Add(new QualityWasteMatrixRow(supplierName, Cell(23m, "C"), Cell(23m, "VISTA"),
                Cell(34m, "C"), Cell(34m, "VISTA"), Cell(44m, "C"), Cell(44m, "VISTA")));
        }
    }

    private void RefreshConsumption(IReadOnlyCollection<DischargeContext> periodDischarges,
        IReadOnlyDictionary<Guid, GroupContext> groupLookup, DateTime from, DateTime to)
    {
        ConsumptionRows.Clear();
        var inclusiveDays = Math.Max(1m, (decimal)(to.Date - from.Date).TotalDays + 1m);
        var periodWeeks = inclusiveDays / 7m;
        foreach (var material in new[] { (23m, "C"), (23m, "VISTA"), (34m, "C"), (34m, "VISTA"), (44m, "C"), (44m, "VISTA") })
        {
            if (SelectedThickness != "Tutti" && material.Item1.ToString("0") != SelectedThickness) continue;
            if (SelectedQuality != "Tutte" && material.Item2 != SelectedQuality) continue;
            var period = periodDischarges.Where(item => item.Context.Group.ConventionalThickness == material.Item1 && item.Context.Group.Quality == material.Item2).Sum(item => item.CubicMeters);
            ConsumptionRows.Add(new ConsumptionStatisticsRow($"{material.Item1:0} {material.Item2}", period,
                period / periodWeeks,
                CalculateRollingWeeklyAverage(4, material.Item1, material.Item2, groupLookup),
                CalculateRollingWeeklyAverage(8, material.Item1, material.Item2, groupLookup)));
        }
    }

    public decimal GetLast8WeeksAverage(decimal conventionalThickness, string quality)
    {
        var groupLookup = _workflow.Loads
            .SelectMany(load => load.Groups.Select(group => new GroupContext(load, group)))
            .ToDictionary(item => item.Group.GroupId);
        return CalculateRollingWeeklyAverage(8, conventionalThickness, quality, groupLookup);
    }

    private decimal CalculateRollingWeeklyAverage(int weeks, decimal conventionalThickness, string quality,
        IReadOnlyDictionary<Guid, GroupContext> groupLookup)
    {
        if (weeks <= 0) return 0m;
        var windowEndExclusive = DateTime.Today.AddDays(1);
        var windowStart = windowEndExclusive.AddDays(-(weeks * 7));
        var total = _inventory.DischargeMovements
            .Where(item => item.DischargeDate >= windowStart && item.DischargeDate < windowEndExclusive)
            .Where(item => groupLookup.TryGetValue(item.MaterialGroupId, out var context)
                && (SelectedSupplier == "Tutti" || context.Load.SupplierName == SelectedSupplier)
                && context.Group.ConventionalThickness == conventionalThickness
                && context.Group.Quality == quality)
            .Sum(item => item.DischargedCubicMeters);
        return total / weeks;
    }

    private void RefreshChartSeries(IReadOnlyCollection<GroupContext> entries,
        IReadOnlyCollection<DischargeContext> discharges, DateTime from, DateTime to)
    {
        TimePoints.Clear();
        var isWeekly = (to.Date - from.Date).TotalDays + 1 <= 45;
        TimeChartGranularity = isWeekly ? "Settimanale" : "Mensile";
        var firstBucket = isWeekly ? StartOfWeek(from) : new DateTime(from.Year, from.Month, 1);
        var lastBucket = isWeekly ? StartOfWeek(to) : new DateTime(to.Year, to.Month, 1);

        for (var bucket = firstBucket; bucket <= lastBucket; bucket = isWeekly ? bucket.AddDays(7) : bucket.AddMonths(1))
        {
            var bucketEnd = isWeekly ? bucket.AddDays(7) : bucket.AddMonths(1);
            var incoming = entries
                .Where(item => item.Load.ArrivalDate >= bucket && item.Load.ArrivalDate < bucketEnd)
                .Sum(item => item.Group.IncomingPhysicalCubicMeters);
            var discharged = discharges
                .Where(item => item.Date >= bucket && item.Date < bucketEnd)
                .Sum(item => item.CubicMeters);
            TimePoints.Add(new StatisticsTimePoint(bucket,
                isWeekly ? bucket.ToString("dd/MM") : bucket.ToString("MMM yyyy"),
                incoming, discharged));
        }

        var incomingPaint = new SolidColorPaint(new SKColor(46, 139, 214));
        var dischargedPaint = new SolidColorPaint(new SKColor(228, 142, 57));
        TimeChartSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Entrate",
                Values = TimePoints.Select(item => item.IncomingCubicMeters).ToArray(),
                Fill = incomingPaint,
                Stroke = null,
                MaxBarWidth = 28,
                YToolTipLabelFormatter = point => $"Entrate: {point.Model:N2} m³"
            },
            new ColumnSeries<decimal>
            {
                Name = "Scarichi",
                Values = TimePoints.Select(item => item.DischargedCubicMeters).ToArray(),
                Fill = dischargedPaint,
                Stroke = null,
                MaxBarWidth = 28,
                YToolTipLabelFormatter = point => $"Scarichi: {point.Model:N2} m³"
            }
        ];

        var labelPaint = new SolidColorPaint(new SKColor(184, 197, 211));
        TimeChartXAxes =
        [
            new Axis
            {
                Labels = TimePoints.Select(item => item.Label).ToArray(),
                LabelsPaint = labelPaint,
                SeparatorsPaint = null,
                TextSize = 11
            }
        ];
        TimeChartYAxes =
        [
            new Axis
            {
                Name = "MC",
                NamePaint = labelPaint,
                LabelsPaint = labelPaint,
                Labeler = value => value.ToString("N2"),
                MinLimit = 0,
                TextSize = 11,
                SeparatorsPaint = new SolidColorPaint(new SKColor(49, 65, 80)) { StrokeThickness = 1 }
            }
        ];

        OnPropertyChanged(nameof(TimeChartSeries));
        OnPropertyChanged(nameof(TimeChartXAxes));
        OnPropertyChanged(nameof(TimeChartYAxes));
        OnPropertyChanged(nameof(TimeChartGranularity));
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-daysSinceMonday);
    }

    private void RefreshSupplierQualityWasteChart(
        IReadOnlyCollection<SupplierQualityWastePoint> points)
    {
        SupplierQualityWastePoints.Clear();
        foreach (var point in points) SupplierQualityWastePoints.Add(point);

        var chartPoints = SupplierQualityWastePoints.ToArray();
        var labelPaint = new SolidColorPaint(new SKColor(184, 197, 211));
        SupplierQualityWasteChartSeries =
        [
            new RowSeries<decimal>
            {
                Name = "Scarto qualità",
                Values = chartPoints.Select(item => item.QualityWastePercentage).ToArray(),
                Fill = new SolidColorPaint(new SKColor(180, 104, 66)),
                Stroke = null,
                MaxBarWidth = 24,
                DataLabelsPaint = new SolidColorPaint(new SKColor(235, 241, 247)),
                DataLabelsSize = 11,
                DataLabelsPosition = DataLabelsPosition.End,
                DataLabelsFormatter = point => $"{point.Model:N2}%",
                XToolTipLabelFormatter = point =>
                {
                    var item = chartPoints[point.Index];
                    return $"{item.SupplierName}\nScarto qualità: {item.QualityWastePercentage:N2}%\n" +
                        $"MC utili teorici analizzati: {item.TheoreticalUsefulCubicMeters:N2} m³\n" +
                        $"MC reali risultanti: {item.RealAvailableCubicMeters:N2} m³";
                }
            }
        ];
        SupplierQualityWasteChartXAxes =
        [
            new Axis
            {
                Name = "Scarto qualità %",
                NamePaint = labelPaint,
                LabelsPaint = labelPaint,
                Labeler = value => $"{value:N2}%",
                MinLimit = 0,
                TextSize = 11,
                SeparatorsPaint = new SolidColorPaint(new SKColor(49, 65, 80)) { StrokeThickness = 1 }
            }
        ];
        SupplierQualityWasteChartYAxes =
        [
            new Axis
            {
                Labels = chartPoints.Select(item => item.SupplierName).ToArray(),
                LabelsPaint = labelPaint,
                SeparatorsPaint = null,
                TextSize = 11,
                IsInverted = true
            }
        ];

        OnPropertyChanged(nameof(SupplierQualityWasteChartSeries));
        OnPropertyChanged(nameof(SupplierQualityWasteChartXAxes));
        OnPropertyChanged(nameof(SupplierQualityWasteChartYAxes));
    }

    private void RefreshSupplierPurchaseChart(IReadOnlyCollection<SupplierPurchasePoint> points)
    {
        SupplierPurchasePoints.Clear();
        foreach (var point in points) SupplierPurchasePoints.Add(point);

        var chartPoints = SupplierPurchasePoints.ToArray();
        var labelPaint = new SolidColorPaint(new SKColor(184, 197, 211));
        SupplierPurchaseChartSeries =
        [
            new RowSeries<decimal>
            {
                Name = "MC acquistati",
                Values = chartPoints.Select(item => item.IncomingCubicMeters).ToArray(),
                Fill = new SolidColorPaint(new SKColor(63, 147, 118)),
                Stroke = null,
                MaxBarWidth = 24,
                DataLabelsPaint = new SolidColorPaint(new SKColor(235, 241, 247)),
                DataLabelsSize = 11,
                DataLabelsPosition = DataLabelsPosition.End,
                DataLabelsFormatter = point => $"{point.Model:N2} m³",
                XToolTipLabelFormatter = point =>
                {
                    var item = chartPoints[point.Index];
                    return $"{item.SupplierName}\nMC acquistati: {item.IncomingCubicMeters:N2} m³\n" +
                        $"Valore acquisti: {item.PurchaseValue:N2} €\n" +
                        $"Prezzo medio ponderato: {item.WeightedAveragePrice:N2} €/m³";
                }
            }
        ];
        SupplierPurchaseChartXAxes =
        [
            new Axis
            {
                Name = "MC acquistati",
                NamePaint = labelPaint,
                LabelsPaint = labelPaint,
                Labeler = value => $"{value:N2}",
                MinLimit = 0,
                TextSize = 11,
                SeparatorsPaint = new SolidColorPaint(new SKColor(49, 65, 80)) { StrokeThickness = 1 }
            }
        ];
        SupplierPurchaseChartYAxes =
        [
            new Axis
            {
                Labels = chartPoints.Select(item => item.SupplierName).ToArray(),
                LabelsPaint = labelPaint,
                SeparatorsPaint = null,
                TextSize = 11,
                IsInverted = true
            }
        ];

        OnPropertyChanged(nameof(SupplierPurchaseChartSeries));
        OnPropertyChanged(nameof(SupplierPurchaseChartXAxes));
        OnPropertyChanged(nameof(SupplierPurchaseChartYAxes));
    }

    private bool MatchesDimensions(GroupContext item) =>
        IsSupplierInScope(item.Load.SupplierName)
        && (SelectedSupplier == "Tutti" || item.Load.SupplierName == SelectedSupplier)
        && (SelectedThickness == "Tutti" || item.Group.ConventionalThickness.ToString("0") == SelectedThickness)
        && (SelectedQuality == "Tutte" || item.Group.Quality == SelectedQuality);

    private static decimal WeightedPercentage(decimal basis, decimal loss) => basis == 0m ? 0m : loss / basis * 100m;

    private sealed record GroupContext(ClassificationLoad Load, MaterialGroupClassification Group);
    private sealed record AdjustmentContext(WasteAdjustment Adjustment, GroupContext Context);
    private sealed record DischargeContext(decimal CubicMeters, DateTime Date, GroupContext Context);
}

public sealed record SupplierStatisticsRow(string SupplierName, decimal PurchasedCubicMeters, decimal Value,
    decimal WeightedAveragePrice, decimal QualityWastePercentage, decimal ProcessingWastePercentage)
{
    public string PurchasedCubicMetersDisplay => PurchasedCubicMeters.ToString("N2");
    public string ValueDisplay => $"{Value:N2} €";
    public string WeightedAveragePriceDisplay => $"{WeightedAveragePrice:N2} €/m³";
    public string QualityWasteDisplay => $"{QualityWastePercentage:N2}%";
    public string ProcessingWasteDisplay => $"{ProcessingWastePercentage:N2}%";
}

public sealed record QualityWasteMatrixRow(string SupplierName, string Thickness23C, string Thickness23Vista,
    string Thickness34C, string Thickness34Vista, string Thickness44C, string Thickness44Vista);

public sealed record ConsumptionStatisticsRow(string Material, decimal DischargedCubicMeters,
    decimal WeeklyAverage, decimal Last4WeeksAverage, decimal Last8WeeksAverage)
{
    public string DischargedDisplay => DischargedCubicMeters.ToString("N2");
    public string WeeklyAverageDisplay => WeeklyAverage.ToString("N2");
    public string Last4WeeksAverageDisplay => Last4WeeksAverage.ToString("N2");
    public string Last8WeeksAverageDisplay => Last8WeeksAverage.ToString("N2");
}

public sealed record StatisticsTimePoint(DateTime PeriodStart, string Label,
    decimal IncomingCubicMeters, decimal DischargedCubicMeters);

public sealed record SupplierQualityWastePoint(string SupplierName,
    decimal TheoreticalUsefulCubicMeters, decimal RealAvailableCubicMeters,
    decimal QualityWastePercentage);

public sealed record SupplierPurchasePoint(string SupplierName, decimal IncomingCubicMeters,
    decimal PurchaseValue, decimal WeightedAveragePrice);
