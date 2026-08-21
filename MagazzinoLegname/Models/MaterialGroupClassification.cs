using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class MaterialGroupClassification : ObservableObject
{
    private DateTime? _classificationDate;
    private string? _classificationOperator;
    private bool _isClassified;

    public Guid GroupId { get; init; } = Guid.NewGuid();
    public required Guid LoadId { get; init; }
    public decimal IncomingThickness { get; init; }
    public decimal ConventionalThickness { get; init; }
    public decimal UsefulThickness { get; init; }
    public decimal IncomingWidth { get; init; }
    public decimal WidthAfterPlaning { get; init; }
    public decimal FinalWidth { get; init; }
    public decimal IncomingLength { get; init; }
    public decimal FinalLength { get; init; }
    public required string Quality { get; init; }
    public int PackageCount { get; init; }
    public int InitialPieces { get; init; }
    public decimal? AppliedPrice { get; init; }
    public decimal? LineValue { get; init; }
    public bool IsLegacyImport { get; init; }
    public decimal? LegacyEstimatedCubicMeters { get; init; }
    public string? LegacyLoadNumber { get; init; }
    public string? LegacyPackageLabel { get; init; }
    public int? LegacyExcelRow { get; init; }
    public string? LegacyQr { get; init; }
    public Guid? LegacyImportBatchId { get; init; }
    public string? LegacyPackageCode { get; init; }
    public int? LegacyPackageNumber { get; init; }
    public int? LegacyTotalPackages { get; init; }
    public decimal IncomingPhysicalCubicMeters =>
        InitialPieces * IncomingThickness * IncomingWidth * IncomingLength / 1_000_000_000m;
    public decimal? TheoreticalUsefulCubicMeters => IsLegacyImport ? null : Volume(InitialPieces);
    public decimal ProcessingWastePercentage => IncomingPhysicalCubicMeters == 0m || !TheoreticalUsefulCubicMeters.HasValue ? 0m
        : (IncomingPhysicalCubicMeters - TheoreticalUsefulCubicMeters.Value)
          / IncomingPhysicalCubicMeters * 100m;
    public DateTime? ClassificationDate { get => _classificationDate; private set => SetProperty(ref _classificationDate, value); }
    public string? ClassificationOperator { get => _classificationOperator; private set => SetProperty(ref _classificationOperator, value); }
    public bool WasteVerified { get; private set; }
    public bool IsClassified => _isClassified;
    public bool CanUndoClassification => IsClassified && !WasteVerified;
    public string ClassificationStatus => IsClassified
        ? WasteVerified ? "Disponibile" : "Classificato · scarti da verificare"
        : "Da classificare";

    public void MarkAsClassified(string operatorName, DateTime classifiedAt)
    {
        if (IsClassified) return;
        ClassificationOperator = operatorName;
        ClassificationDate = classifiedAt;
        _isClassified = true;
        WasteVerified = false;
        OnPropertyChanged(nameof(IsClassified));
        OnPropertyChanged(nameof(CanUndoClassification));
        OnPropertyChanged(nameof(ClassificationStatus));
        OnPropertyChanged(nameof(WasteVerified));
    }

    public bool UndoClassification()
    {
        if (!CanUndoClassification) return false;
        ClassificationOperator = null;
        ClassificationDate = null;
        _isClassified = false;
        OnPropertyChanged(nameof(IsClassified));
        OnPropertyChanged(nameof(CanUndoClassification));
        OnPropertyChanged(nameof(ClassificationStatus));
        return true;
    }

    public void MarkAsLegacyClassified(DateTime? classifiedAt)
    {
        if (IsClassified) return;
        ClassificationDate = classifiedAt;
        ClassificationOperator = null;
        _isClassified = true;
        WasteVerified = false;
        OnPropertyChanged(nameof(IsClassified)); OnPropertyChanged(nameof(ClassificationStatus)); OnPropertyChanged(nameof(WasteVerified));
    }

    public decimal Volume(int pieces) => pieces * UsefulThickness * FinalWidth * FinalLength / 1_000_000_000m;

    public void MarkWasteAsVerified()
    {
        if (!IsClassified || WasteVerified) return;
        WasteVerified = true;
        OnPropertyChanged(nameof(WasteVerified));
        OnPropertyChanged(nameof(CanUndoClassification));
        OnPropertyChanged(nameof(ClassificationStatus));
    }
}
