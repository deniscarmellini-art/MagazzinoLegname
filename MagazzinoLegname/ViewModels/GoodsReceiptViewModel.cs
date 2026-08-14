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
    private Supplier? _selectedSupplier;
    private string _deliveryNoteNumber = string.Empty;
    private string _selectedOperator;
    private GoodsReceiptLine? _selectedLine;
    private bool _pricesCaptured;
    private int _expectedPackages = 12;
    private GoodsReceiptLoadDraft _loadDraft = new();
    private GoodsReceiptRegistrationState _registrationState = GoodsReceiptRegistrationState.New;
    private bool _isBusy;
    private string _validationMessage = string.Empty;

    public GoodsReceiptViewModel()
    {
        Suppliers = _supplierCatalog.Suppliers;
        Operators = ["Andrea Rossi", "Elena Bianchi", "Marco Conti"];
        _selectedSupplier = null;
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
    public Supplier? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (SetProperty(ref _selectedSupplier, value))
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
        : SelectedSupplier is null ? "—"
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
    public GoodsReceiptRegistrationState RegistrationState
    {
        get => _registrationState;
        private set { if (SetProperty(ref _registrationState, value)) OnPropertyChanged(nameof(IsRegistered)); }
    }
    public bool IsRegistered => RegistrationState == GoodsReceiptRegistrationState.RegisteredAwaitingPrint;
    public bool IsBusy
    {
        get => _isBusy;
        set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(IsPrimaryActionEnabled)); }
    }
    public bool IsPrimaryActionEnabled => !IsBusy;
    public string ValidationMessage { get => _validationMessage; private set => SetProperty(ref _validationMessage, value); }
    public IReadOnlyList<PhysicalPackageDraft> RegisteredPackages { get; private set; } = [];

    public bool ValidateForRegistration()
    {
        var errors = new List<string>();
        if (!EntryDate.HasValue) errors.Add("Selezionare una Data entrata valida.");
        if (SelectedSupplier is null) errors.Add("Selezionare il Fornitore.");
        if (DisplayLoadNumber == "—") errors.Add("Il Numero carico non è stato generato.");
        if (string.IsNullOrWhiteSpace(DeliveryNoteNumber)) errors.Add("Inserire il Numero DDT.");
        if (string.IsNullOrWhiteSpace(SelectedOperator)) errors.Add("Selezionare l'Operatore.");
        if (TotalPackages <= 0) errors.Add("Inserire almeno un pacco.");
        if (TotalPackages != ExpectedPackages) errors.Add($"I pacchi inseriti ({TotalPackages}) devono coincidere con i pacchi previsti ({ExpectedPackages}).");
        foreach (var (line, index) in Lines.Select((line, index) => (line, index)))
        {
            if (line.PackageCount <= 0) errors.Add($"Riga {index + 1}: N° pacchi deve essere maggiore di zero.");
            if (line.PiecesPerPackage <= 0) errors.Add($"Riga {index + 1}: Pezzi/pacco deve essere maggiore di zero.");
            if (line.IncomingThickness <= 0m) errors.Add($"Riga {index + 1}: Spessore non valido.");
            if (line.IncomingWidth <= 0m) errors.Add($"Riga {index + 1}: Larghezza non valida.");
            if (line.IncomingLength <= 0m) errors.Add($"Riga {index + 1}: Lunghezza non valida.");
            if (!GoodsReceiptLine.AllowedQualities.Contains(line.Quality)) errors.Add($"Riga {index + 1}: Qualità non valida.");
        }
        ValidationMessage = errors.Count == 0 ? string.Empty : string.Join(Environment.NewLine, errors);
        return errors.Count == 0;
    }
    public void SetOperationError(string message) => ValidationMessage = message;

    public void CaptureRegistrationSnapshot()
    {
        if (RegistrationState != GoodsReceiptRegistrationState.New || SelectedSupplier is null) return;
        _pricesCaptured = true;
        if (!_loadDraft.IsNumberAssigned)
            _loadDraft.AssignNumber(_loadNumberSequence.ReserveNext(SelectedSupplier.Id,
                (EntryDate ?? DateTime.Today).Year), SelectedSupplier.Code);
        _loadDraft.DeliveryNoteNumber = DeliveryNoteNumber;
        _loadDraft.CaptureCertification(_generalSettings.Settings.DefaultTimberCertification);
        OnPropertyChanged(nameof(CertificationIndicator)); OnPropertyChanged(nameof(DisplayLoadNumber));
    }

    public void MarkRegistered(IReadOnlyList<PhysicalPackageDraft> packages)
    {
        RegisteredPackages = packages;
        RegistrationState = GoodsReceiptRegistrationState.RegisteredAwaitingPrint;
        ValidationMessage = string.Empty;
    }

    public void CompleteAndReset(bool keepOperator = true)
    {
        RegistrationState = GoodsReceiptRegistrationState.Completed;
        Reset(keepOperator);
    }

    public void Reset(bool keepOperator = true)
    {
        var previousOperator = SelectedOperator;
        _pricesCaptured = false;
        _loadDraft = new GoodsReceiptLoadDraft();
        OnPropertyChanged(nameof(LoadDraft)); OnPropertyChanged(nameof(CertificationIndicator)); OnPropertyChanged(nameof(DisplayLoadNumber));
        EntryDate = DateTime.Today;
        ExpectedPackages = 12;
        SelectedSupplier = null;
        DeliveryNoteNumber = string.Empty;
        SelectedOperator = keepOperator ? previousOperator : Operators[0];
        foreach (var line in Lines) line.PropertyChanged -= Line_PropertyChanged;
        Lines.Clear();
        RegisteredPackages = [];
        RegistrationState = GoodsReceiptRegistrationState.New;
        ValidationMessage = string.Empty;
        AddLine();
    }

    private void AddLine()
    {
        var line = new GoodsReceiptLine { PackageCount = 0 };
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
        var configuration = SelectedSupplier is null ? null : _supplierCatalog.GetConfiguration(SelectedSupplier.Id, thickness);
        var price = _pricesCaptured ? line.PrezzoApplicato : EntryDate.HasValue
            && SelectedSupplier is not null
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
