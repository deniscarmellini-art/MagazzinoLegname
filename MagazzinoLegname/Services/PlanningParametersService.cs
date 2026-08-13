using System.Collections.ObjectModel;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class PlanningParametersService
{
    private static readonly Lazy<PlanningParametersService> SharedInstance = new(() => new());
    private PlanningParametersService()
    {
        WeeklyValues =
        [
            new(23m) { TheoreticalCubicMetersPerExpectedArrival = 28m, ExpectedWeeklyConsumption = 14m },
            new(34m) { TheoreticalCubicMetersPerExpectedArrival = 32m, ExpectedWeeklyConsumption = 18m },
            new(44m) { TheoreticalCubicMetersPerExpectedArrival = 36m, ExpectedWeeklyConsumption = 21m }
        ];
    }
    public static PlanningParametersService Shared => SharedInstance.Value;
    public ObservableCollection<MaterialPlanningParameter> WeeklyValues { get; }
}
