using System.Collections.ObjectModel;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class PlanningDataService
{
    public static PlanningDataService Shared { get; } = new();
    private PlanningDataService() { }

    public ObservableCollection<PlannedArrival> Arrivals { get; } = [];
    public ObservableCollection<PlannedConsumption> Consumptions { get; } = [];
    public event EventHandler? PlanningChanged;

    public PlannedArrival GetOrCreateArrival(Guid supplierId, DateTime date)
    {
        var arrival = Arrivals.FirstOrDefault(item => item.SupplierId == supplierId && item.Date.Date == date.Date);
        if (arrival is not null) return arrival;
        arrival = new PlannedArrival { SupplierId = supplierId, Date = date.Date };
        arrival.PropertyChanged += (_, _) => PlanningChanged?.Invoke(this, EventArgs.Empty);
        Arrivals.Add(arrival);
        return arrival;
    }

    public PlannedConsumption GetOrCreateConsumption(DateTime weekStart, decimal thickness, string quality)
    {
        var consumption = Consumptions.FirstOrDefault(item => item.WeekStart.Date == weekStart.Date
            && item.ConventionalThickness == thickness && item.Quality == quality);
        if (consumption is not null) return consumption;
        consumption = new PlannedConsumption
        {
            WeekStart = weekStart.Date, ConventionalThickness = thickness, Quality = quality
        };
        consumption.PropertyChanged += (_, _) => PlanningChanged?.Invoke(this, EventArgs.Empty);
        Consumptions.Add(consumption);
        return consumption;
    }
}
