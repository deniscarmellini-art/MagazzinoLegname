using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class GoodsReceiptView : UserControl
{
    private GoodsReceiptViewModel ViewModel => (GoodsReceiptViewModel)DataContext;
    private readonly PackageExpansionService _packageExpansionService = new();
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

    private void RegisterAndPrint_Click(object sender, RoutedEventArgs e)
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
                var packages = _packageExpansionService.Expand(ViewModel.LoadDraft,
                    ViewModel.EntryDate!.Value, ViewModel.Lines);
                _registrationService.Register(ViewModel.LoadDraft, ViewModel.SelectedSupplier!,
                    ViewModel.SelectedOperator, ViewModel.EntryDate.Value, ViewModel.Lines, packages);
                ViewModel.MarkRegistered(packages);
            }

            if (ViewModel.IsRegistered && ShowLabelPreview(ViewModel.RegisteredPackages) == true)
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

    private bool? ShowLabelPreview(IReadOnlyList<PhysicalPackageDraft> packages)
    {
        var preview = new QrPackagePreviewWindow(packages, ViewModel.SelectedSupplier!.Name,
            ViewModel.LoadDraft.LoadNumber, ViewModel.LoadDraft.DeliveryNoteNumber,
            ViewModel.LoadDraft.CertificationApplied) { Owner = Window.GetWindow(this) };
        return preview.ShowDialog();
    }
}
