using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class MaterialParametersService
{
    private static readonly Lazy<MaterialParametersService> SharedInstance = new(() => new());
    private MaterialParametersService() => Parameters.PropertyChanged += (_, _) => ParametersChanged?.Invoke(this, EventArgs.Empty);
    public static MaterialParametersService Shared => SharedInstance.Value;
    public MaterialParameters Parameters { get; } = new();
    public event EventHandler? ParametersChanged;
    public void NotifyChanged() => ParametersChanged?.Invoke(this, EventArgs.Empty);
}
