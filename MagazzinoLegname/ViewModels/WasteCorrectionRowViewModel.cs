using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.ViewModels;

public sealed class WasteCorrectionRowViewModel : ObservableObject
{
    private int _discardedWholeBoards;
    private decimal _partialWastePercentage;

    public WasteCorrectionRowViewModel(ClassificationLoad load, MaterialGroupClassification group)
    {
        Load = load; Group = group;
    }

    public ClassificationLoad Load { get; }
    public MaterialGroupClassification Group { get; }
    public string LoadNumber => Load.LoadNumber;
    public string SupplierName => Load.SupplierName;
    public DateTime ArrivalDate => Load.ArrivalDate;
    public int DiscardedWholeBoards
    {
        get => _discardedWholeBoards;
        set
        {
            if (!SetProperty(ref _discardedWholeBoards, Math.Clamp(value, 0, Group.InitialPieces))) return;
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
    public int GoodPieces => Group.InitialPieces - DiscardedWholeBoards;
    public decimal CubicMetersAfterWholeBoardWaste => Group.Volume(GoodPieces);
    public decimal PartialWasteCubicMeters => CubicMetersAfterWholeBoardWaste * PartialWastePercentage / 100m;
    public decimal RealAvailableCubicMeters => CubicMetersAfterWholeBoardWaste - PartialWasteCubicMeters;
    public decimal WholeBoardWastePercentage => Group.InitialPieces == 0 ? 0m
        : (decimal)DiscardedWholeBoards / Group.InitialPieces * 100m;
    public decimal TotalClassificationWastePercentage => Group.TheoreticalUsefulCubicMeters == 0m ? 0m
        : (Group.TheoreticalUsefulCubicMeters - RealAvailableCubicMeters)
          / Group.TheoreticalUsefulCubicMeters * 100m;

    public WasteAdjustment CreateSnapshot(string operatorName, DateTime date) => new()
    {
        LoadId = Load.Id, MaterialGroupId = Group.GroupId, AdjustmentDate = date,
        AdjustmentOperator = operatorName, InitialPieces = Group.InitialPieces,
        DiscardedWholeBoards = DiscardedWholeBoards, GoodPieces = GoodPieces,
        TheoreticalUsefulCubicMeters = Group.TheoreticalUsefulCubicMeters,
        CubicMetersAfterWholeBoardWaste = CubicMetersAfterWholeBoardWaste,
        PartialWastePercentage = PartialWastePercentage,
        PartialWasteCubicMeters = PartialWasteCubicMeters,
        RealAvailableCubicMeters = RealAvailableCubicMeters,
        WholeBoardWastePercentage = WholeBoardWastePercentage,
        TotalClassificationWastePercentage = TotalClassificationWastePercentage
    };

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
