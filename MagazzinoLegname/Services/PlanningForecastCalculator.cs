using MagazzinoLegname.Models;
namespace MagazzinoLegname.Services;

public sealed record PlanningWeeklyForecast(DateTime WeekStart, decimal OpeningBalance, decimal ExpectedArrivals,
    decimal ExpectedConsumption, decimal ClosingBalance);

public static class PlanningForecastCalculator
{
    public static IReadOnlyList<PlanningWeeklyForecast> Calculate(decimal currentInventory, decimal thickness, string quality,
        DateTime weekA, DateTime weekB, IEnumerable<PlannedArrival> arrivals, IEnumerable<PlannedConsumption> consumptions)
    {
        if (weekA.DayOfWeek != DayOfWeek.Monday || weekB.DayOfWeek != DayOfWeek.Monday || weekB.Date <= weekA.Date)
            throw new ArgumentException("Selezionare i lunedì delle settimane A e B in ordine cronologico.");
        var incoming = arrivals.Where(x => x.Status == PlannedArrivalStatus.Expected
            && x.ConventionalThickness == thickness && x.Quality == quality).ToArray();
        var outgoing = consumptions.Where(x => x.ConventionalThickness == thickness && x.Quality == quality).ToArray();
        var result = new List<PlanningWeeklyForecast>(2);
        var opening = currentInventory;
        foreach (var monday in new[] { weekA.Date, weekB.Date })
        {
            // The lower grid is a weekly plan, not a daily or historical stock reconstruction.
            var added = incoming.Where(x => x.Date.Date >= monday && x.Date.Date <= monday.AddDays(4))
                .Sum(x => x.ExpectedCubicMeters);
            var used = outgoing.SingleOrDefault(x => x.WeekStart == monday)?.ExpectedCubicMeters ?? 0;
            var closing = opening + added - used;
            result.Add(new PlanningWeeklyForecast(monday, opening, added, used, closing));
            opening = closing;
        }
        return result;
    }
}
