using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class PlanningSettingsService
{
    public static PlanningSettingsService Shared { get; } = new();
    private PlanningSettingsService() => Settings.PropertyChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);

    public PlanningSettings Settings => SqlDomainConfigurationState.Shared.Planning;
    public event EventHandler? SettingsChanged;
    public void NotifyChanged() { SqlDomainConfigurationState.Shared.Save(); SettingsChanged?.Invoke(this, EventArgs.Empty); }
    public void Reload() { SqlDomainConfigurationState.Shared.Reload(); SettingsChanged?.Invoke(this, EventArgs.Empty); }
}
