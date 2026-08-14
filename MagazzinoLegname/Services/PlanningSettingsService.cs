using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class PlanningSettingsService
{
    public static PlanningSettingsService Shared { get; } = new();
    private PlanningSettingsService() => Settings.PropertyChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);

    public PlanningSettings Settings { get; } = new();
    public event EventHandler? SettingsChanged;
    public void NotifyChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);
}
