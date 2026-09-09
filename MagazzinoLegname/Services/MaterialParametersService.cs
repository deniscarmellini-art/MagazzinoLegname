using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class MaterialParametersService
{
    private static readonly Lazy<MaterialParametersService> SharedInstance = new(() => new());
    private MaterialParametersService() => Parameters.PropertyChanged += (_, _) => ParametersChanged?.Invoke(this, EventArgs.Empty);
    public static MaterialParametersService Shared => SharedInstance.Value;
    public MaterialParameters Parameters => SqlDomainConfigurationState.Shared.Material;
    public event EventHandler? ParametersChanged;
    public void NotifyChanged() { SqlDomainConfigurationState.Shared.Save(); ParametersChanged?.Invoke(this, EventArgs.Empty); }
    public void Reload() { SqlDomainConfigurationState.Shared.Reload(); ParametersChanged?.Invoke(this, EventArgs.Empty); }
}
