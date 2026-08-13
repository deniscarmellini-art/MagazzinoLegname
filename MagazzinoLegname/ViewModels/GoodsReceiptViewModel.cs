using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class GoodsReceiptViewModel : ObservableObject
{
    private readonly GoodsReceiptCalculationService _calculationService = new();
    private readonly SupplierCatalogService _supplierCatalog = SupplierCatalogService.Shared;
    private readonly MaterialParametersService _materialParameters = MaterialParametersService.Shared;
    private readonly GeneralSettingsService _generalSettings = GeneralSettingsService.Shared;
    private readonly LoadNumberSequenceService _loadNumberSequence = LoadNumberSequenceService.Shared;
    private DateTime? _entryDate = DateTime.Today;
    private Supplier _selectedSupplier;
    private string _deliveryNoteNumber = string.Empty;
    private string _selectedOperator;
    private GoodsReceiptLine? _selectedLine;
    private bool _pricesCaptured;
    private int _expectedPackages = 12;
    private GoodsReceiptLoadDraft _loadDraft = new();

    public GoodsReceiptViewModel()
    {
        Suppliers = _supplierCatalog.Suppliers;
        Operators = ["Andrea Rossi", "Elena Bianchi", "Marco Conti"];
        _selectedSupplier = Suppliers.First(supplier => supplier.IsActive);
        _selectedOperator = Operators[0];
        _supplierCatalog.CatalogChanged += SupplierCatalog_CatalogChanged;
        _materialParameters.ParametersChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SupplierProcessingLabel));
            RecalculateAll();
        };
        _generalSettings.SettingsChanged += (_, _) =>
        {
            if (!_loadDraft.IsCertificationCaptured) OnPropertyChanged(nameof(CertificationIndicator));
        };
        AddLineCommand = new RelayCommand(AddLine);
        DeleteLineCommand = new RelayCommand(DeleteSelectedLine, () => SelectedLine is not null);
        DuplicateLineCommand = new RelayCommand(DuplicateSelectedLine, () => SelectedLine is not null);
        AddLine();
    }

    public ObservableCollection<Supplier> Suppliers { get; }
    public ObservableCollection<string> Operators { get; }
    public ObservableCollection<GoodsReceiptLine> Lines { get; } = [];
    public DateTime? EntryDate
    {
        get => _entryDate;
        set { if (SetProperty(ref _entryDate, value)) { OnPropertyChanged(nameof(DisplayLoadNumber)); RecalculateAll(); } }
    }
    public Supplier SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (value is not null && SetProperty(ref _selectedSupplier, value))
            {
                OnPropertyChanged(nameof(SupplierProcessingLabel));
                OnPropertyChanged(nameof(DisplayLoadNumber));
                RecalculateAll();
            }
        }
    }
    public string SupplierProcessingLabel => "Prepiallatura e riduzioni dimensionali applicate per fornitore e famiglia";
    public string DeliveryNoteNumber { get => _deliveryNoteNumber; set => SetProperty(ref _deliveryNoteNumber, value); }
    public string DisplayLoadNumber => _loadDraft.IsNumberAssigned ? _loadDraft.LoadNumber
        : _loadNumberSequence.PreviewNext(SelectedSupplier.Id, (EntryDate ?? DateTime.Today).Year).LoadNumber;
    public string SelectedOperator { get => _selectedOperator; set => SetProperty(ref _selectedOperator, value); }
    public GoodsReceiptLine? SelectedLine
    {
        get => _selectedLine;
        set
        {
            if (!SetProperty(ref _selectedLine, value)) return;
            ((RelayCommand)DeleteLineCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DuplicateLineCommand).RaiseCanExecuteChanged();
        }
    }
    public ICommand AddLineCommand { get; }
    public ICommand DeleteLineCommand { get; }
    public ICommand DuplicateLineCommand { get; }
    public int ExpectedPackages
    {
        get => _expectedPackages;
        set { if (SetProperty(ref _expectedPackages, Math.Max(1, value))) NotifyPackageControlChanged(); }
    }
    public int TotalPackages => Lines.Sum(line => line.PackageCount);
    public int TotalPieces => Lines.Sum(line => line.TotalPieces);
    public decimal TotalArrivedCubicMeters => Lines.Sum(line => line.PhysicalIncomingCubicMeters);
    public decimal TotalTheoreticalCubicMeters => Lines.Sum(line => line.TheoreticalUsefulCubicMeters);
    public decimal TotalRealAvailableCubicMeters => Lines.Sum(line => line.RealAvailableUsefulCubicMeters);
    public decimal TotalProcessingLossCubicMeters => Lines.Sum(line => line.ProcessingLossCubicMeters);
    public decimal TotalProcessingLossPercentage => TotalArrivedCubicMeters > 0m
        ? TotalProcessingLossCubicMeters / TotalArrivedCubicMeters * 100m : 0m;
    public decimal TotalValue => Lines.Sum(line => line.LineValue);
    public GoodsReceiptLoadDraft LoadDraft => _loadDraft;
    public string CertificationIndicator => _loadDraft.IsCertificationCaptured
        ? _loadDraft.CertificationApplied : _generalSettings.Settings.DefaultTimberCertification;
    public int PackageDifference => TotalPackages - ExpectedPackages;
    public bool AreExpectedPackagesComplete => PackageDifference == 0;
    public bool ArePackagesMissing => PackageDifference < 0;
    public bool ArePackagesExceeding => PackageDifference > 0;
    public string PackageControlText => $"Pacchi inseriti: {TotalPackages} / {ExpectedPackages}";
    public string PackageControlDetail => PackageDifference switch
    {
        0 => "Quantità completa ✓",
        < 0 => $"Mancano {-PackageDifference} {(-PackageDifference == 1 ? "pacco" : "pacchi")}",
        _ => $"{PackageDifference} {(PackageDifference == 1 ? "pacco in eccesso" : "pacchi in eccesso")}"
    };
    public bool IsReceiptValid => Lines.Count > 0 && Lines.All(line => line.IsValid);

    public void CaptureRegistrationSnapshot()
    {
        _pricesCaptured = true;
        if (!_loadDraft.IsNumberAssigned)
            _loadDraft.AssignNumber(_loadNumberSequence.ReserveNext(SelectedSupplier.Id,
                (EntryDate ?? DateTime.Today).Year), SelectedSupplier.Code);
        _loadDraft.DeliveryNoteNumber = DeliveryNoteNumber;
        _loadDraft.CaptureCertification(_generalSettings.Settings.DefaultTimberCertification);
        OnPropertyChanged(nameof(CertificationIndicator)); OnPropertyChanged(nameof(DisplayLoadNumber));
    }

    public void Reset()
    {
        _pricesCaptured = false;
        _loadDraft = new GoodsReceiptLoadDraft();
        OnPropertyChanged(nameof(LoadDraft)); OnPropertyChanged(nameof(CertificationIndicator)); OnPropertyChanged(nameof(DisplayLoadNumber));
        EntryDate = DateTime.Today;
        ExpectedPackages = 12;
        SelectedSupplier = Suppliers.First(supplier => supplier.IsActive);
        DeliveryNoteNumber = string.Empty;
        SelectedOperator = Operators[0];
        foreach (var line in Lines) line.PropertyChanged -= Line_PropertyChanged;
        Lines.Clear();
        AddLine();
    }

    private void AddLine()
    {
        var line = new GoodsReceiptLine();
        line.PropertyChanged += Line_PropertyChanged;
        Lines.Add(line);
        Recalculate(line);
        SelectedLine = line;
        NotifyTotalsChanged();
    }
    private void DeleteSelectedLine()
    {
        if (SelectedLine is null) return;
        SelectedLine.PropertyChanged -= Line_PropertyChanged;
        Lines.Remove(SelectedLine);
        SelectedLine = Lines.LastOrDefault();
        NotifyTotalsChanged();
    }
    private void DuplicateSelectedLine()
    {
        if (SelectedLine is null) return;
        var duplicate = SelectedLine.DuplicateInputs();
        duplicate.PropertyChanged += Line_PropertyChanged;
        Lines.Add(duplicate);
        Recalculate(duplicate);
        SelectedLine = duplicate;
        NotifyTotalsChanged();
    }
    private void Line_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GoodsReceiptLine line || e.PropertyName is not
            (nameof(GoodsReceiptLine.PackageCount) or nameof(GoodsReceiptLine.PiecesPerPackage)
            or nameof(GoodsReceiptLine.IncomingThickness) or nameof(GoodsReceiptLine.IncomingWidth)
            or nameof(GoodsReceiptLine.IncomingLength) or nameof(GoodsReceiptLine.DiscardedPieces)
            or nameof(GoodsReceiptLine.IsClassified))) return;
        Recalculate(line);
        NotifyTotalsChanged();
    }
    private void Recalculate(GoodsReceiptLine line)
    {
        var thickness = GoodsReceiptCalculationService.GetConventionalThickness(line.IncomingThickness, _materialParameters.Parameters);
        var configuration = _supplierCatalog.GetConfiguration(SelectedSupplier.Id, thickness);
        var price = _pricesCaptured ? line.PrezzoApplicato : EntryDate.HasValue
            ? _supplierCatalog.GetValidPrice(SelectedSupplier.Id, thickness, EntryDate.Value) ?? 0m
            : 0m;
        _calculationService.Recalculate(line, configuration, _materialParameters.Parameters, price);
    }
    private void RecalculateAll()
    {
        foreach (var line in Lines) Recalculate(line);
        NotifyTotalsChanged();
    }
    private void SupplierCatalog_CatalogChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SupplierProcessingLabel));
        RecalculateAll();
    }
    private void NotifyTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalPackages)); OnPropertyChanged(nameof(TotalPieces));
        OnPropertyChanged(nameof(TotalArrivedCubicMeters)); OnPropertyChanged(nameof(TotalTheoreticalCubicMeters));
        OnPropertyChanged(nameof(TotalRealAvailableCubicMeters)); OnPropertyChanged(nameof(TotalValue));
        OnPropertyChanged(nameof(TotalProcessingLossCubicMeters)); OnPropertyChanged(nameof(TotalProcessingLossPercentage));
        OnPropertyChanged(nameof(IsReceiptValid));
        NotifyPackageControlChanged();
    }
    private void NotifyPackageControlChanged()
    {
        OnPropertyChanged(nameof(PackageDifference)); OnPropertyChanged(nameof(AreExpectedPackagesComplete));
        OnPropertyChanged(nameof(ArePackagesMissing)); OnPropertyChanged(nameof(ArePackagesExceeding));
        OnPropertyChanged(nameof(PackageControlText)); OnPropertyChanged(nameof(PackageControlDetail));
    }
}
