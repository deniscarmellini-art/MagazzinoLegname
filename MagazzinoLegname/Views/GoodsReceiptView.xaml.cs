using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class GoodsReceiptView : UserControl
{
    private GoodsReceiptViewModel ViewModel => (GoodsReceiptViewModel)DataContext;
    private readonly GoodsReceiptRegistrationService _registrationService = new();

    public GoodsReceiptView()
    {
        InitializeComponent();
        DataContext = new GoodsReceiptViewModel();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var wasRegistered = ViewModel.IsRegistered;
        ViewModel.CompleteAndReset();
        ViewModel.SetOperationError(wasRegistered
            ? "Fase di stampa abbandonata. Il carico registrato rimane nello storico e in giacenza."
            : string.Empty);
    }

    private void RegisterLoad_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy) return;
        if (ViewModel.RegistrationState == GoodsReceiptRegistrationState.New
            && !ViewModel.ValidateForRegistration()) return;

        ViewModel.IsBusy = true;
        try
        {
            if (ViewModel.RegistrationState == GoodsReceiptRegistrationState.New)
            {
                ViewModel.CaptureRegistrationSnapshot();
                var load = _registrationService.Register(ViewModel.LoadDraft, ViewModel.SelectedSupplier!,
                    ViewModel.SelectedOperator, ViewModel.EntryDate!.Value, ViewModel.ExpectedPackages, ViewModel.Lines.ToList());
                ViewModel.MarkRegistered(ClassificationWorkflowService.Shared.RegisteredPhysicalPackages
                    .Where(x => x.LoadId == load.Id).OrderBy(x => x.SequenceNumber).ToList());
            }

            if (ViewModel.IsRegistered)
                ViewModel.CompleteAndReset();
        }
        catch (Exception exception)
        {
            ViewModel.SetOperationError($"Registrazione non completata: {exception.Message}");
        }
        finally
        {
            ViewModel.IsBusy = false;
        }
    }
}
