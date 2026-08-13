using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class ThicknessFamilyConfiguration : ObservableObject
{
    private decimal _minimumIncomingThickness;
    private decimal _maximumIncomingThickness;
    private decimal _conventionalThickness;
    private decimal _usefulProductionThickness;
    private decimal _standardWidthReductionMillimeters;
    private decimal _fingerJointLengthReductionMillimeters;

    public ThicknessFamilyConfiguration(decimal minimum, decimal maximum, decimal conventional, decimal useful,
        decimal standardWidthReductionMillimeters = 10m, decimal fingerJointLengthReductionMillimeters = 10m)
    {
        _minimumIncomingThickness = minimum;
        _maximumIncomingThickness = maximum;
        _conventionalThickness = conventional;
        _usefulProductionThickness = useful;
        _standardWidthReductionMillimeters = standardWidthReductionMillimeters;
        _fingerJointLengthReductionMillimeters = fingerJointLengthReductionMillimeters;
    }
    public decimal MinimumIncomingThickness { get => _minimumIncomingThickness; set => SetProperty(ref _minimumIncomingThickness, Math.Max(0m, value)); }
    public decimal MaximumIncomingThickness { get => _maximumIncomingThickness; set => SetProperty(ref _maximumIncomingThickness, Math.Max(0m, value)); }
    public decimal ConventionalThickness { get => _conventionalThickness; set => SetProperty(ref _conventionalThickness, Math.Max(0m, value)); }
    public decimal UsefulProductionThickness { get => _usefulProductionThickness; set => SetProperty(ref _usefulProductionThickness, Math.Max(0m, value)); }
    public decimal StandardWidthReductionMillimeters { get => _standardWidthReductionMillimeters; set => SetProperty(ref _standardWidthReductionMillimeters, Math.Max(0m, value)); }
    public decimal FingerJointLengthReductionMillimeters { get => _fingerJointLengthReductionMillimeters; set => SetProperty(ref _fingerJointLengthReductionMillimeters, Math.Max(0m, value)); }
    public bool Includes(decimal incomingThickness) => incomingThickness >= MinimumIncomingThickness && incomingThickness <= MaximumIncomingThickness;
    public string FamilyLabel => $"Famiglia {MinimumIncomingThickness:0}-{MaximumIncomingThickness:0}";
}
