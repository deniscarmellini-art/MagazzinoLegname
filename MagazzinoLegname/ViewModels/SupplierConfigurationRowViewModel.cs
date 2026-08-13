using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.ViewModels;

public sealed class SupplierConfigurationRowViewModel(SupplierThicknessConfiguration configuration) : ObservableObject
{
    private decimal _newPrice;
    private DateTime? _validFrom = DateTime.Today;
    public SupplierThicknessConfiguration Configuration { get; } = configuration;
    public decimal NewPrice { get => _newPrice; set => SetProperty(ref _newPrice, Math.Max(0m, value)); }
    public DateTime? ValidFrom { get => _validFrom; set => SetProperty(ref _validFrom, value); }
}
