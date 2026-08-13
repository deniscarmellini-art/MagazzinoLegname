using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class SupplierThicknessConfiguration : ObservableObject
{
    private bool _isPlaningEnabled;
    private decimal _planingReductionMillimeters;
    private decimal _currentPrice;

    public SupplierThicknessConfiguration(Guid supplierId, decimal conventionalThickness,
        bool isPlaningEnabled, decimal planingReductionMillimeters)
    {
        SupplierId = supplierId;
        ConventionalThickness = conventionalThickness;
        _isPlaningEnabled = isPlaningEnabled;
        _planingReductionMillimeters = isPlaningEnabled ? planingReductionMillimeters : 0m;
    }

    public Guid SupplierId { get; }
    public decimal ConventionalThickness { get; }
    public bool IsPlaningEnabled
    {
        get => _isPlaningEnabled;
        set
        {
            if (!SetProperty(ref _isPlaningEnabled, value)) return;
            PlaningReductionMillimeters = value ? 5m : 0m;
            OnPropertyChanged(nameof(EffectivePlaningReductionMillimeters));
        }
    }
    public decimal PlaningReductionMillimeters
    {
        get => _planingReductionMillimeters;
        set
        {
            var normalized = IsPlaningEnabled ? Math.Max(0m, value) : 0m;
            if (SetProperty(ref _planingReductionMillimeters, normalized))
                OnPropertyChanged(nameof(EffectivePlaningReductionMillimeters));
        }
    }
    public decimal EffectivePlaningReductionMillimeters => IsPlaningEnabled ? PlaningReductionMillimeters : 0m;
    public decimal CurrentPrice { get => _currentPrice; internal set => SetProperty(ref _currentPrice, value); }
}
