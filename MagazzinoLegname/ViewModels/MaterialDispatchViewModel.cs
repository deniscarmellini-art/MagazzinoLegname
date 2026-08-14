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

    public MaterialDispatchViewModel()
    {
        Operators = ["Andrea Rossi", "Elena Bianchi", "Marco Conti"];
        _selectedOperator = Operators[0];
    }

    public ObservableCollection<string> Operators { get; }
    public ObservableCollection<MaterialDischargeMovement> RecentDischarges { get; } = [];
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
            OnPropertyChanged(nameof(PreviousDischargeDetail));
        }
    }
    public InventoryPackage? CurrentPackage => LookupResult?.Package;
    public bool HasPackage => CurrentPackage is not null;
    public bool CanDischarge => LookupResult?.CanDischarge == true;
    public string FeedbackMessage { get => _feedbackMessage; private set => SetProperty(ref _feedbackMessage, value); }
    public bool IsSuccessFeedback { get => _isSuccessFeedback; private set => SetProperty(ref _isSuccessFeedback, value); }
    public string DischargeAmountText => CurrentPackage is null ? string.Empty
        : $"Verranno scaricati {CurrentPackage.InventoryCubicMeters:N6} m³";
    public string PreviousDischargeDetail => LookupResult?.PreviousMovement is not { } movement
        ? string.Empty
        : $"Scaricato il {movement.DischargeDate:dd/MM/yyyy HH:mm} da {movement.DischargeOperator} · {movement.DischargedCubicMeters:N6} m³";

    public void Scan()
    {
        LookupResult = _service.Lookup(QrInput);
        FeedbackMessage = LookupResult.Message;
        IsSuccessFeedback = LookupResult.CanDischarge;
    }

    public bool ConfirmDischarge()
    {
        if (!CanDischarge || CurrentPackage is null) return false;
        try
        {
            var movement = _service.Confirm(CurrentPackage, SelectedOperator);
            RecentDischarges.Insert(0, movement);
            while (RecentDischarges.Count > 10) RecentDischarges.RemoveAt(RecentDischarges.Count - 1);
            FeedbackMessage = $"Pacco {movement.PackageCode} scaricato correttamente · MC scaricati: {movement.DischargedCubicMeters:N6}";
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
}
