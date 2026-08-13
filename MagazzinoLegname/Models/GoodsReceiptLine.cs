using MagazzinoLegname.Infrastructure;
using System.ComponentModel;

namespace MagazzinoLegname.Models;

public sealed class GoodsReceiptLine : ObservableObject, IDataErrorInfo
{
    private int _packageCount = 1;
    private int _piecesPerPackage = 1;
    private int _discardedPieces;
    private decimal _incomingThickness = 34m;
    private decimal _incomingWidth = 180m;
    private decimal _incomingLength = 4000m;
    private decimal _conventionalThickness;
    private decimal _usefulProductionThickness;
    private decimal _planingReduction;
    private decimal _standardWidthReduction;
    private decimal _finalWidth;
    private decimal _fingerJointLengthReduction;
    private decimal _finalLength;
    private decimal _physicalIncomingCubicMeters;
    private decimal _theoreticalUsefulCubicMeters;
    private decimal _realAvailableUsefulCubicMeters;
    private decimal _processingLossCubicMeters;
    private decimal _processingLossPercentage;
    private decimal _appliedPrice;
    private decimal _lineValue;
    private bool _isClassified;
    private string _quality = "C";

    public static IReadOnlyList<string> AllowedQualities { get; } = ["C", "VISTA"];

    public Guid GroupId { get; init; } = Guid.NewGuid();

    public int PackageCount { get => _packageCount; set => SetProperty(ref _packageCount, Math.Max(0, value)); }
    public string Quality
    {
        get => _quality;
        set
        {
            if (!AllowedQualities.Contains(value)) return;
            SetProperty(ref _quality, value);
        }
    }
    public int PiecesPerPackage { get => _piecesPerPackage; set => SetProperty(ref _piecesPerPackage, Math.Max(0, value)); }
    public int EnteredPieces => PackageCount * PiecesPerPackage;
    public int DiscardedPieces { get => _discardedPieces; set => SetProperty(ref _discardedPieces, Math.Clamp(value, 0, EnteredPieces)); }
    public int GoodPieces => Math.Max(0, EnteredPieces - DiscardedPieces);
    public bool IsClassified { get => _isClassified; set => SetProperty(ref _isClassified, value); }

    public decimal IncomingThickness { get => _incomingThickness; set => SetProperty(ref _incomingThickness, Math.Max(0m, value)); }
    public decimal ConventionalThickness { get => _conventionalThickness; internal set => SetProperty(ref _conventionalThickness, value); }
    public decimal UsefulProductionThickness { get => _usefulProductionThickness; internal set => SetProperty(ref _usefulProductionThickness, value); }
    public decimal IncomingWidth { get => _incomingWidth; set => SetProperty(ref _incomingWidth, Math.Max(0m, value)); }
    public decimal PlaningReduction { get => _planingReduction; internal set => SetProperty(ref _planingReduction, value); }
    public decimal StandardWidthReduction { get => _standardWidthReduction; internal set => SetProperty(ref _standardWidthReduction, value); }
    public decimal FinalWidth { get => _finalWidth; internal set => SetProperty(ref _finalWidth, value); }
    public decimal IncomingLength { get => _incomingLength; set => SetProperty(ref _incomingLength, Math.Max(0m, value)); }
    public decimal FingerJointLengthReduction { get => _fingerJointLengthReduction; internal set => SetProperty(ref _fingerJointLengthReduction, value); }
    public decimal FinalLength { get => _finalLength; internal set => SetProperty(ref _finalLength, value); }

    public decimal PhysicalIncomingCubicMeters { get => _physicalIncomingCubicMeters; internal set => SetProperty(ref _physicalIncomingCubicMeters, value); }
    public decimal TheoreticalUsefulCubicMeters { get => _theoreticalUsefulCubicMeters; internal set => SetProperty(ref _theoreticalUsefulCubicMeters, value); }
    public decimal RealAvailableUsefulCubicMeters { get => _realAvailableUsefulCubicMeters; internal set => SetProperty(ref _realAvailableUsefulCubicMeters, value); }
    public decimal ProcessingLossCubicMeters { get => _processingLossCubicMeters; internal set => SetProperty(ref _processingLossCubicMeters, value); }
    public decimal ProcessingLossPercentage { get => _processingLossPercentage; internal set => SetProperty(ref _processingLossPercentage, value); }
    public decimal PrezzoApplicato { get => _appliedPrice; internal set => SetProperty(ref _appliedPrice, value); }
    public decimal PricePerCubicMeter => PrezzoApplicato;
    public decimal LineValue { get => _lineValue; internal set => SetProperty(ref _lineValue, value); }
    public bool IsValid => PackageCount > 0 && PiecesPerPackage > 0 && IncomingThickness > 0m
        && IncomingWidth > 0m && IncomingLength > 0m;
    public string Error => string.Empty;
    public string this[string columnName] => columnName switch
    {
        nameof(PackageCount) when PackageCount <= 0 => "Il numero di pacchi deve essere maggiore di zero.",
        nameof(PiecesPerPackage) when PiecesPerPackage <= 0 => "I pezzi per pacco devono essere maggiori di zero.",
        nameof(IncomingThickness) when IncomingThickness <= 0m => "Lo spessore deve essere maggiore di zero.",
        nameof(IncomingWidth) when IncomingWidth <= 0m => "La larghezza deve essere maggiore di zero.",
        nameof(IncomingLength) when IncomingLength <= 0m => "La lunghezza deve essere maggiore di zero.",
        _ => string.Empty
    };

    public GoodsReceiptLine DuplicateInputs() => new()
    {
        PackageCount = PackageCount,
        PiecesPerPackage = PiecesPerPackage,
        IncomingThickness = IncomingThickness,
        IncomingWidth = IncomingWidth,
        IncomingLength = IncomingLength,
        Quality = Quality
    };

    public IEnumerable<PhysicalPackageDraft> ExpandToPhysicalPackages(Guid loadId, int firstSequenceNumber)
    {
        for (var index = 0; index < PackageCount; index++)
            yield return new PhysicalPackageDraft(Guid.NewGuid(), loadId, GroupId,
                firstSequenceNumber + index, PiecesPerPackage, IncomingThickness, IncomingWidth,
                WidthAfterPlaning, IncomingLength, Quality)
                { PackageCode = string.Empty, TotalPackages = 0, ArrivalDate = default,
                    QrPayload = string.Empty };
    }

    // Alias temporanei per compatibilità con viste/servizi già predisposti.
    public int TotalPieces => EnteredPieces;
    public decimal Length { get => IncomingLength; set => IncomingLength = value; }
    public decimal WidthAfterPlaning => Math.Max(0m, IncomingWidth - PlaningReduction);
    public decimal TheoreticalWidth => FinalWidth;
    public decimal ArrivedCubicMeters => PhysicalIncomingCubicMeters;
    public decimal TheoreticalCubicMeters => TheoreticalUsefulCubicMeters;
    public decimal RealAvailableCubicMeters => RealAvailableUsefulCubicMeters;

    internal void NotifyDerivedPieceCountsChanged()
    {
        OnPropertyChanged(nameof(EnteredPieces)); OnPropertyChanged(nameof(TotalPieces));
        OnPropertyChanged(nameof(GoodPieces));
        OnPropertyChanged(nameof(IsValid));
    }
}
