using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class InventoryViewModel : ObservableObject
{
    private readonly InventoryProjectionService _projection = InventoryProjectionService.Shared;
    private IReadOnlyList<InventoryPackage> _allPackages = [];
    private string _selectedSupplier = "Tutti";
    private string _selectedThickness = "Tutti";
    private string _selectedWidth = "Tutte";
    private string _selectedQuality = "Tutte";
    private string _selectedClassificationStatus = "Tutti";
    private string _selectedWasteStatus = "Tutti";

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
    public string SelectedSupplier { get => _selectedSupplier; set { if (SetProperty(ref _selectedSupplier, value)) ApplyFilters(); } }
    public string SelectedThickness { get => _selectedThickness; set { if (SetProperty(ref _selectedThickness, value)) ApplyFilters(); } }
    public string SelectedWidth { get => _selectedWidth; set { if (SetProperty(ref _selectedWidth, value)) ApplyFilters(); } }
    public string SelectedQuality { get => _selectedQuality; set { if (SetProperty(ref _selectedQuality, value)) ApplyFilters(); } }
    public string SelectedClassificationStatus { get => _selectedClassificationStatus; set { if (SetProperty(ref _selectedClassificationStatus, value)) ApplyFilters(); } }
    public string SelectedWasteStatus { get => _selectedWasteStatus; set { if (SetProperty(ref _selectedWasteStatus, value)) ApplyFilters(); } }
    public int PresentPackages => VisiblePackages.Count;
    public decimal InventoryCubicMeters => VisiblePackages.Sum(package => package.InventoryCubicMeters);
    public decimal CubicMetersToConsolidate => VisiblePackages.Where(package => !package.UsesRealCubicMeters).Sum(package => package.InventoryCubicMeters);
    public decimal RealCubicMeters => VisiblePackages.Where(package => package.UsesRealCubicMeters).Sum(package => package.InventoryCubicMeters);
    public decimal InventoryValue => VisiblePackages.Sum(package => package.PackageValue);

    public void RemovePackage(InventoryPackage package, string operatorName, string reason, string? note)
    {
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

        _allPackages = _projection.BuildInventory();
        ReplaceOptions(Suppliers, "Tutti", _allPackages.Select(package => package.SupplierName));
        ReplaceOptions(Thicknesses, "Tutti", _allPackages.Select(package => package.ConventionalThickness.ToString("0")));
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
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var matches = _allPackages
            .Where(package => SelectedSupplier == "Tutti" || package.SupplierName == SelectedSupplier)
            .Where(package => SelectedThickness == "Tutti" || package.ConventionalThickness.ToString("0") == SelectedThickness)
            .Where(package => SelectedWidth == "Tutte" || package.WidthAfterPlaning.ToString("0") == SelectedWidth)
            .Where(package => SelectedQuality == "Tutte" || package.Quality == SelectedQuality)
            .Where(package => SelectedClassificationStatus == "Tutti" || package.ClassificationStatus == SelectedClassificationStatus)
            .Where(package => SelectedWasteStatus == "Tutti"
                || SelectedWasteStatus == "Rettificato" && package.UsesRealCubicMeters
                || SelectedWasteStatus == "Da rettificare" && !package.UsesRealCubicMeters)
            .ToList();
        VisiblePackages.Clear();
        foreach (var package in matches) VisiblePackages.Add(package);
        OnPropertyChanged(nameof(PresentPackages)); OnPropertyChanged(nameof(InventoryCubicMeters));
        OnPropertyChanged(nameof(CubicMetersToConsolidate)); OnPropertyChanged(nameof(RealCubicMeters));
        OnPropertyChanged(nameof(InventoryValue));
    }

    private static void ReplaceOptions(ObservableCollection<string> target, string allLabel, IEnumerable<string> values)
    {
        target.Clear(); target.Add(allLabel);
        foreach (var value in values.Distinct().OrderBy(value => value)) target.Add(value);
    }

    private void RestoreSelection(ref string field, string propertyName,
        IEnumerable<string> availableOptions, string? previousValue, string fallback)
    {
        field = previousValue is not null && availableOptions.Contains(previousValue)
            ? previousValue
            : fallback;
        OnPropertyChanged(propertyName);
    }
}
