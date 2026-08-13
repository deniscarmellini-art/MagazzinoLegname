using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class MaterialPlanningParameter(decimal conventionalThickness) : ObservableObject
{
    private decimal _theoreticalCubicMetersPerExpectedArrival;
    private decimal _expectedWeeklyConsumption;
    public decimal ConventionalThickness { get; } = conventionalThickness;
    public decimal TheoreticalCubicMetersPerExpectedArrival { get => _theoreticalCubicMetersPerExpectedArrival; set => SetProperty(ref _theoreticalCubicMetersPerExpectedArrival, Math.Max(0m, value)); }
    public decimal ExpectedWeeklyConsumption { get => _expectedWeeklyConsumption; set => SetProperty(ref _expectedWeeklyConsumption, Math.Max(0m, value)); }
}
