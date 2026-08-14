using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class PlannedConsumption : ObservableObject
{
    private decimal _cubicMeters;

    public Guid Id { get; init; } = Guid.NewGuid();
    public required DateTime WeekStart { get; init; }
    public required decimal ConventionalThickness { get; init; }
    public required string Quality { get; init; }
    public decimal CubicMeters { get => _cubicMeters; set => SetProperty(ref _cubicMeters, Math.Max(0m, value)); }
}
