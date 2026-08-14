using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class PlanningSettings : ObservableObject
{
    private decimal _standardCubicMetersPerExpectedLoad23 = 50m;
    private decimal _standardCubicMetersPerExpectedLoad34 = 50m;
    private decimal _standardCubicMetersPerExpectedLoad44 = 50m;

    public decimal StandardCubicMetersPerExpectedLoad23
    {
        get => _standardCubicMetersPerExpectedLoad23;
        set => SetProperty(ref _standardCubicMetersPerExpectedLoad23, Math.Max(0m, value));
    }

    public decimal StandardCubicMetersPerExpectedLoad34
    {
        get => _standardCubicMetersPerExpectedLoad34;
        set => SetProperty(ref _standardCubicMetersPerExpectedLoad34, Math.Max(0m, value));
    }

    public decimal StandardCubicMetersPerExpectedLoad44
    {
        get => _standardCubicMetersPerExpectedLoad44;
        set => SetProperty(ref _standardCubicMetersPerExpectedLoad44, Math.Max(0m, value));
    }

    public decimal GetStandardCubicMetersPerExpectedLoad(decimal conventionalThickness) =>
        conventionalThickness switch
        {
            23m => StandardCubicMetersPerExpectedLoad23,
            34m => StandardCubicMetersPerExpectedLoad34,
            44m => StandardCubicMetersPerExpectedLoad44,
            _ => 0m
        };
}
