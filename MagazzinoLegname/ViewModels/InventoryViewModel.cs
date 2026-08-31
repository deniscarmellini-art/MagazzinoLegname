using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class InventoryViewModel : ObservableObject
{
    private readonly InventoryProjectionService _projection = InventoryProjectionService.Shared;
    private readonly MaterialParameters _materialParameters = MaterialParametersService.Shared.Parameters;
    private IReadOnlyList<InventoryPackage> _allPackages = [];
    private string _selectedSupplier = "Tutti";
    private string _selectedThickness = "Tutti";
    private string _selectedWidth = "Tutte";
    private string _selectedQuality = "Tutte";
    private string _selectedClassificationStatus = "Tutti";
    private string _selectedWasteStatus = "Tutti";
    private string _selectedPackageType = "Tutti";

    public InventoryViewModel()
    {
        ClassificationWorkflowService.Shared.WorkflowChanged += (_, _) => Reload();
        _projection.InventoryChanged += (_, _) => Reload();
        Reload();
    }

    public ObservableCollection<InventoryPackage> VisiblePackages { get; } = [];
    public ObservableCollection<string> Suppliers { get; } = [];
    public ObservableCollection<string> Thicknesses { get; } = [];
    public ObservableCollection<string> Widths { get; } = [];
    public ObservableCollection<string> Qualities { get; } = [];
    public IReadOnlyList<string> ClassificationStatuses { get; } = ["Tutti", "Da classificare", "Classificato"];
    public IReadOnlyList<string> WasteStatuses { get; } = ["Tutti", "Da rettificare", "Rettificato"];
    public IReadOnlyList<string> PackageTypes { get; } = ["Tutti", "Ufficiali", "Supplementari"];
    public string SelectedSupplier { get => _selectedSupplier; set { if (SetProperty(ref _selectedSupplier, value)) ApplyFilters(); } }
    public string SelectedThickness { get => _selectedThickness; set { if (SetProperty(ref _selectedThickness, value)) ApplyFilters(); } }
    public string SelectedWidth { get => _selectedWidth; set { if (SetProperty(ref _selectedWidth, value)) ApplyFilters(); } }
    public string SelectedQuality { get => _selectedQuality; set { if (SetProperty(ref _selectedQuality, value)) ApplyFilters(); } }
    public string SelectedClassificationStatus { get => _selectedClassificationStatus; set { if (SetProperty(ref _selectedClassificationStatus, value)) ApplyFilters(); } }
    public string SelectedWasteStatus { get => _selectedWasteStatus; set { if (SetProperty(ref _selectedWasteStatus, value)) ApplyFilters(); } }
    public string SelectedPackageType { get => _selectedPackageType; set { if (SetProperty(ref _selectedPackageType, value)) ApplyFilters(); } }
    public int PresentPackages => VisiblePackages.Count;
    public decimal InventoryCubicMeters => VisiblePackages.Where(package => package.IsAccountedPackage).Sum(package => package.InventoryCubicMeters);
    public decimal CubicMetersToConsolidate => VisiblePackages.Where(package => package.IsAccountedPackage && !package.UsesRealCubicMeters).Sum(package => package.InventoryCubicMeters);
    public decimal RealCubicMeters => VisiblePackages.Where(package => package.IsAccountedPackage && package.UsesRealCubicMeters).Sum(package => package.InventoryCubicMeters);
    public decimal InventoryValue => VisiblePackages.Where(package => package.IsAccountedPackage).Sum(package => package.PackageValue ?? 0m);
    public int AccountedPackages => VisiblePackages.Count(package => package.IsAccountedPackage);
    public int PackagesWithoutPrice => VisiblePackages.Count(package => package.IsAccountedPackage && !package.AppliedPrice.HasValue);
    public string InventoryValueDisplay => AccountedPackages == 0 || PackagesWithoutPrice == AccountedPackages ? "N/D"
        : $"{InventoryValue:N2} €" + (PackagesWithoutPrice > 0 ? " · PARZIALE" : "");

    public IReadOnlyList<InventoryPackage> GetVisiblePackagesSnapshot() => VisiblePackages.ToArray();

    public void RemovePackage(InventoryPackage package, string operatorName, string reason, string? note)
    {
        if (package.IsSupplementary) throw new InvalidOperationException("I pacchi supplementari devono uscire tramite Scarico supplementare.");
        if (reason == "Reso")
            SupplierReturnService.Shared.ReturnPackages(package.LoadId, [package.PackageCode], operatorName,
                "Reso a fornitore", note);
        else
            _projection.RemovePackage(package.PackageCode, operatorName, reason, note);
        Reload();
    }

    private void Reload()
    {
        var previousSupplier = _selectedSupplier;
        var previousThickness = _selectedThickness;
        var previousWidth = _selectedWidth;
        var previousQuality = _selectedQuality;
        var previousClassificationStatus = _selectedClassificationStatus;
        var previousWasteStatus = _selectedWasteStatus;
        var previousPackageType = _selectedPackageType;

        _allPackages = _projection.BuildInventory();
        ReplaceOptions(Suppliers, "Tutti", _allPackages.Select(package => package.SupplierName));
        ReplaceOptions(Thicknesses, "Tutti", _allPackages.Select(package => ConventionalThickness(package).ToString("0")));
        ReplaceOptions(Widths, "Tutte", _allPackages.Select(package => package.WidthAfterPlaning.ToString("0")));
        ReplaceOptions(Qualities, "Tutte", _allPackages.Select(package => package.Quality));

        RestoreSelection(ref _selectedSupplier, nameof(SelectedSupplier), Suppliers, previousSupplier, "Tutti");
        RestoreSelection(ref _selectedThickness, nameof(SelectedThickness), Thicknesses, previousThickness, "Tutti");
        RestoreSelection(ref _selectedWidth, nameof(SelectedWidth), Widths, previousWidth, "Tutte");
        RestoreSelection(ref _selectedQuality, nameof(SelectedQuality), Qualities, previousQuality, "Tutte");
        RestoreSelection(ref _selectedClassificationStatus, nameof(SelectedClassificationStatus),
            ClassificationStatuses, previousClassificationStatus, "Tutti");
        RestoreSelection(ref _selectedWasteStatus, nameof(SelectedWasteStatus),
            WasteStatuses, previousWasteStatus, "Tutti");
        RestoreSelection(ref _selectedPackageType, nameof(SelectedPackageType),
            PackageTypes, previousPackageType, "Tutti");
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var matches = _allPackages
            .Where(package => SelectedSupplier == "Tutti" || package.SupplierName == SelectedSupplier)
            .Where(package => SelectedThickness == "Tutti" || ConventionalThickness(package).ToString("0") == SelectedThickness)
            .Where(package => SelectedWidth == "Tutte" || package.WidthAfterPlaning.ToString("0") == SelectedWidth)
            .Where(package => SelectedQuality == "Tutte" || package.Quality == SelectedQuality)
            .Where(package => SelectedClassificationStatus == "Tutti" || package.ClassificationStatus == SelectedClassificationStatus)
            .Where(package => SelectedWasteStatus == "Tutti"
                || SelectedWasteStatus == "Rettificato" && package.IsAccountedPackage && package.UsesRealCubicMeters
                || SelectedWasteStatus == "Da rettificare" && package.IsAccountedPackage && !package.UsesRealCubicMeters)
            .Where(package => SelectedPackageType == "Tutti"
                || SelectedPackageType == "Ufficiali" && package.PackageType == PackageType.Official
                || SelectedPackageType == "Supplementari" && package.PackageType == PackageType.Supplementary)
            .ToList();
        VisiblePackages.Clear();
        foreach (var package in matches) VisiblePackages.Add(package);
        OnPropertyChanged(nameof(PresentPackages)); OnPropertyChanged(nameof(InventoryCubicMeters));
        OnPropertyChanged(nameof(CubicMetersToConsolidate)); OnPropertyChanged(nameof(RealCubicMeters));
        OnPropertyChanged(nameof(InventoryValue)); OnPropertyChanged(nameof(AccountedPackages));
        OnPropertyChanged(nameof(PackagesWithoutPrice)); OnPropertyChanged(nameof(InventoryValueDisplay));
    }

    private static void ReplaceOptions(ObservableCollection<string> target, string allLabel, IEnumerable<string> values)
    {
        target.Clear(); target.Add(allLabel);
        foreach (var value in values.Distinct().OrderBy(value => value)) target.Add(value);
    }
    private decimal ConventionalThickness(InventoryPackage package) => _materialParameters.FindFamily(package.IncomingThickness)?.ConventionalThickness ?? 0m;

    private void RestoreSelection(ref string field, string propertyName,
        IEnumerable<string> availableOptions, string? previousValue, string fallback)
    {
        field = previousValue is not null && availableOptions.Contains(previousValue)
            ? previousValue
            : fallback;
        OnPropertyChanged(propertyName);
    }
}