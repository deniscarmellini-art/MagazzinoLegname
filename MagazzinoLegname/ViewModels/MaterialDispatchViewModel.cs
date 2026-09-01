using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class MaterialDispatchViewModel : ObservableObject
{
    private readonly MaterialDischargeService _service = new();
    private string _qrInput = string.Empty;
    private string _selectedOperator;
    private PackageLookupResult? _lookupResult;
    private string _feedbackMessage = "Pronto per la scansione";
    private bool _isSuccessFeedback;
    private bool _scanInProgress;

    public MaterialDispatchViewModel()
    {
        Operators = OperatorCatalogService.Shared.ActiveOperatorNames;
        _selectedOperator = Operators.FirstOrDefault() ?? string.Empty;
        OperatorCatalogService.Shared.CatalogChanged += (_, _) => EnsureActiveSelection();
    }

    public ReadOnlyObservableCollection<string> Operators { get; }
    public ObservableCollection<PackageExitResult> RecentDischarges { get; } = [];
    public string QrInput { get => _qrInput; set => SetProperty(ref _qrInput, value); }
    public string SelectedOperator { get => _selectedOperator; set => SetProperty(ref _selectedOperator, value); }
    public PackageLookupResult? LookupResult
    {
        get => _lookupResult;
        private set
        {
            if (!SetProperty(ref _lookupResult, value)) return;
            OnPropertyChanged(nameof(CurrentPackage)); OnPropertyChanged(nameof(HasPackage));
            OnPropertyChanged(nameof(CanDischarge)); OnPropertyChanged(nameof(DischargeAmountText));
            OnPropertyChanged(nameof(PreviousDischargeDetail)); OnPropertyChanged(nameof(ConfirmButtonText));
        }
    }
    public InventoryPackage? CurrentPackage => LookupResult?.Package;
    public bool HasPackage => CurrentPackage is not null;
    public bool CanDischarge => LookupResult?.CanDischarge == true;
    public string ConfirmButtonText => CurrentPackage?.IsSupplementary == true ? "CONFERMA USCITA" : "CONFERMA SCARICO";
    public string FeedbackMessage { get => _feedbackMessage; private set => SetProperty(ref _feedbackMessage, value); }
    public bool IsSuccessFeedback { get => _isSuccessFeedback; private set => SetProperty(ref _isSuccessFeedback, value); }
    public string DischargeAmountText => CurrentPackage is null ? string.Empty
        : CurrentPackage.IsSupplementary ? "Uscita fisica senza movimento di MC."
        : $"Verranno scaricati {CurrentPackage.InventoryCubicMeters:N6} m³";
    public string PreviousDischargeDetail => LookupResult?.PreviousMovement is not { } movement
        ? string.Empty
        : $"Scaricato il {movement.DischargeDate:dd/MM/yyyy HH:mm} da {movement.DischargeOperator} · {movement.DischargedCubicMeters:N6} m³";

    public PackageLookupResult Scan()
    {
        if (_scanInProgress)
            return new(PackageLookupStatus.InvalidQr, "Scansione già in elaborazione.");
        _scanInProgress = true;
        try
        {
            var result = _service.Lookup(QrInput);
            FeedbackMessage = result.Message;
            IsSuccessFeedback = result.CanDischarge;
            LookupResult = result.CanDischarge ? result : null;
            if (!result.CanDischarge) QrInput = string.Empty;
            return result;
        }
        finally
        {
            _scanInProgress = false;
        }
    }

    public bool ConfirmDischarge()
    {
        if (!CanDischarge || CurrentPackage is null) return false;
        try
        {
            var movement = _service.Confirm(CurrentPackage, SelectedOperator);
            RecentDischarges.Insert(0, movement);
            while (RecentDischarges.Count > 10) RecentDischarges.RemoveAt(RecentDischarges.Count - 1);
            FeedbackMessage = movement.Message;
            IsSuccessFeedback = true;
            LookupResult = null;
            QrInput = string.Empty;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            FeedbackMessage = exception.Message;
            IsSuccessFeedback = false;
            LookupResult = null;
            QrInput = string.Empty;
            return false;
        }
    }

    public void Cancel()
    {
        LookupResult = null;
        QrInput = string.Empty;
        FeedbackMessage = "Operazione annullata. Pronto per la scansione successiva.";
        IsSuccessFeedback = false;
    }

    private void EnsureActiveSelection()
    {
        if (!Operators.Contains(SelectedOperator)) SelectedOperator = Operators.FirstOrDefault() ?? string.Empty;
    }
}
