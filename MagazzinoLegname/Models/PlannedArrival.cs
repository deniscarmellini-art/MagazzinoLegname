using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class PlannedArrival : ObservableObject
{
    private decimal? _conventionalThickness;
    private string? _quality;
    private int _loadQuantity;

    public Guid Id { get; init; } = Guid.NewGuid();
    public required DateTime Date { get; init; }
    public required Guid SupplierId { get; init; }
    public decimal? ConventionalThickness { get => _conventionalThickness; set => SetProperty(ref _conventionalThickness, value); }
    public string? Quality { get => _quality; set => SetProperty(ref _quality, value); }
    public int LoadQuantity { get => _loadQuantity; set => SetProperty(ref _loadQuantity, Math.Max(0, value)); }
}
