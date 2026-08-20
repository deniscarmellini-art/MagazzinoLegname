using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class PlannedArrival : ObservableObject
{
    private decimal? _conventionalThickness;
    private string? _quality;
    private int _loadQuantity;
    private PlannedArrivalStatus _status = PlannedArrivalStatus.Expected;
    private DateTime? _confirmedAt;
    private string? _confirmedBy;

    public Guid Id { get; init; } = Guid.NewGuid();
    public required DateTime Date { get; init; }
    public required Guid SupplierId { get; init; }
    public decimal? ConventionalThickness { get => _conventionalThickness; set => SetProperty(ref _conventionalThickness, value); }
    public string? Quality { get => _quality; set => SetProperty(ref _quality, value); }
    public int LoadQuantity { get => _loadQuantity; set => SetProperty(ref _loadQuantity, Math.Max(0, value)); }
    public PlannedArrivalStatus Status { get => _status; private set => SetProperty(ref _status, value); }
    public DateTime? ConfirmedAt { get => _confirmedAt; private set => SetProperty(ref _confirmedAt, value); }
    public string? ConfirmedBy { get => _confirmedBy; private set => SetProperty(ref _confirmedBy, value); }

    public void Confirm(DateTime confirmedAt, string? confirmedBy)
    {
        if (Status == PlannedArrivalStatus.Confirmed || LoadQuantity <= 0) return;
        ConfirmedAt = confirmedAt;
        ConfirmedBy = string.IsNullOrWhiteSpace(confirmedBy) ? null : confirmedBy.Trim();
        Status = PlannedArrivalStatus.Confirmed;
    }
}

public enum PlannedArrivalStatus
{
    Expected,
    Confirmed
}
