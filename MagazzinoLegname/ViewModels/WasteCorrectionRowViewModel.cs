using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MagazzinoLegname.ViewModels;

public sealed class WasteCorrectionRowViewModel : ObservableObject
{
    private int _discardedWholeBoards;
    private decimal _partialWastePercentage;
    private string _partialWastePercentageText = "0";
    private string _selectedOperator = string.Empty;
    private readonly WasteAdjustmentCalculationService _calculationService = new();

    public WasteCorrectionRowViewModel(ClassificationLoad load, MaterialGroupClassification group,
        IReadOnlyCollection<InventoryPackage> presentPackages)
    {
        Load = load; Group = group;
        InitialPieces = presentPackages.Sum(package =>
            ClassificationWorkflowService.Shared.RegisteredPhysicalPackages
                .FirstOrDefault(item => item.PackageCode.Equals(package.PackageCode, StringComparison.OrdinalIgnoreCase))?.PieceCount
            ?? (group.PackageCount == 0 ? 0 : group.InitialPieces / group.PackageCount));
        AdjustmentBaseCubicMeters = presentPackages.Sum(package => package.IncomingCubicMeters);
    }

    public ClassificationLoad Load { get; }
    public MaterialGroupClassification Group { get; }
    public string LoadNumber => Load.LoadNumber;
    public string SupplierName => Load.SupplierName;
    public DateTime ArrivalDate => Load.ArrivalDate;
    public int InitialPieces { get; }
    public decimal AdjustmentBaseCubicMeters { get; }
    public string SelectedOperator
    {
        get => _selectedOperator;
        set => SetProperty(ref _selectedOperator, value);
    }
    public int DiscardedWholeBoards
    {
        get => _discardedWholeBoards;
        set
        {
            if (!SetProperty(ref _discardedWholeBoards, Math.Clamp(value, 0, InitialPieces))) return;
            NotifyCalculations();
        }
    }
    public decimal PartialWastePercentage
    {
        get => _partialWastePercentage;
        set
        {
            if (!SetProperty(ref _partialWastePercentage, Math.Clamp(value, 0m, 100m))) return;
            NotifyCalculations();
        }
    }
    public string PartialWastePercentageText
    {
        get => _partialWastePercentageText;
        set
        {
            var normalized = (value ?? string.Empty).Replace('.', ',');
            if (!Regex.IsMatch(normalized, @"^\d{0,3}(,\d?)?$") || !IsWithinPercentageRange(normalized))
            {
                OnPropertyChanged();
                return;
            }

            if (!SetProperty(ref _partialWastePercentageText, normalized)) return;
            if (normalized.Length == 0 || normalized.EndsWith(',')) return;
            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out var parsed)) return;
            PartialWastePercentage = parsed;
        }
    }

    public void CommitPartialWastePercentageText()
    {
        var formatted = PartialWastePercentage.ToString("0.#", CultureInfo.GetCultureInfo("it-IT"));
        SetProperty(ref _partialWastePercentageText, formatted, nameof(PartialWastePercentageText));
    }

    private static bool IsWithinPercentageRange(string text)
    {
        if (text.Length == 0 || text == ",") return true;
        var valueToParse = text.EndsWith(',') ? text[..^1] : text;
        return valueToParse.Length == 0 || decimal.TryParse(valueToParse, NumberStyles.Number,
            CultureInfo.GetCultureInfo("it-IT"), out var value) && value is >= 0m and <= 100m;
    }
    private WasteAdjustmentCalculation Calculation =>
        _calculationService.Calculate(AdjustmentBaseCubicMeters, InitialPieces,
            DiscardedWholeBoards, PartialWastePercentage);
    public int GoodPieces => Calculation.GoodGroupPieces;
    public decimal CubicMetersAfterWholeBoardWaste => Calculation.CubicMetersAfterWholeBoardWaste;
    public decimal PartialWasteCubicMeters => Calculation.PartialWasteCubicMeters;
    public decimal RealAvailableCubicMeters => Calculation.RealAvailableCubicMeters;
    public decimal WholeBoardWastePercentage => Calculation.WholeBoardWastePercentage;
    public decimal TotalClassificationWastePercentage => Calculation.TotalQualityWastePercentage;

    public WasteAdjustment CreateSnapshot(string operatorName, DateTime date)
    {
        var result = Calculation;
        return new()
        {
            LoadId = Load.Id, MaterialGroupId = Group.GroupId, AdjustmentDate = date,
            AdjustmentOperator = operatorName, InitialPieces = result.InitialGroupPieces,
            DiscardedWholeBoards = result.DiscardedWholeBoards, GoodPieces = result.GoodGroupPieces,
            AdjustmentBaseCubicMeters = result.AdjustmentBaseCubicMeters,
            // Campo mantenuto per compatibilità: da ora contiene la base fisica della rettifica.
            TheoreticalUsefulCubicMeters = result.AdjustmentBaseCubicMeters,
            CubicMetersAfterWholeBoardWaste = result.CubicMetersAfterWholeBoardWaste,
            PartialWastePercentage = result.PartialWastePercentage,
            PartialWasteCubicMeters = result.PartialWasteCubicMeters,
            RealAvailableCubicMeters = result.RealAvailableCubicMeters,
            WholeBoardWastePercentage = result.WholeBoardWastePercentage,
            TotalClassificationWastePercentage = result.TotalQualityWastePercentage
        };
    }

    private void NotifyCalculations()
    {
        OnPropertyChanged(nameof(GoodPieces));
        OnPropertyChanged(nameof(CubicMetersAfterWholeBoardWaste));
        OnPropertyChanged(nameof(PartialWasteCubicMeters));
        OnPropertyChanged(nameof(RealAvailableCubicMeters));
        OnPropertyChanged(nameof(WholeBoardWastePercentage));
        OnPropertyChanged(nameof(TotalClassificationWastePercentage));
    }
}
