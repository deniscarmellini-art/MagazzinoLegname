using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class GeneralSettingsService
{
    private static readonly Lazy<GeneralSettingsService> SharedInstance = new(() => new());
    private GeneralSettingsService() => Settings.PropertyChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
    public static GeneralSettingsService Shared => SharedInstance.Value;
    public GeneralSettings Settings => SqlDomainConfigurationState.Shared.General;
    public event EventHandler? SettingsChanged;
    public void NotifyChanged() { SqlDomainConfigurationState.Shared.Save(); SettingsChanged?.Invoke(this, EventArgs.Empty); }
    public void Reload() { SqlDomainConfigurationState.Shared.Reload(); SettingsChanged?.Invoke(this, EventArgs.Empty); }
}
