using System.Windows.Controls;
using System.Windows;
using MagazzinoLegname.ViewModels;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.Views;

public partial class GoodsReceiptView : UserControl
{
    private GoodsReceiptViewModel ViewModel => (GoodsReceiptViewModel)DataContext;
    private readonly PackageExpansionService _packageExpansionService = new();

    public GoodsReceiptView()
    {
        InitializeComponent();
        DataContext = new GoodsReceiptViewModel();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Reset();
        MessageBox.Show("I dati locali del carico sono stati azzerati.", "Entrata merce",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Register_Click(object sender, RoutedEventArgs e) => ShowDemoRegistrationMessage(false);

    private void RegisterAndPrint_Click(object sender, RoutedEventArgs e) => ShowDemoRegistrationMessage(true);

    private void ShowDemoRegistrationMessage(bool includeLabels)
    {
        ViewModel.CaptureRegistrationSnapshot();
        var physicalPackages = _packageExpansionService.Expand(ViewModel.LoadDraft,
            ViewModel.EntryDate ?? DateTime.Today, ViewModel.Lines);
        var preview = new QrPackagePreviewWindow(physicalPackages, ViewModel.SelectedSupplier.Name,
            ViewModel.LoadDraft.LoadNumber) { Owner = Window.GetWindow(this) };
        preview.ShowDialog();
        var action = includeLabels ? "Registrazione e stampa etichette" : "Registrazione entrata";
        MessageBox.Show(
            $"{action} predisposta.\n\nCarico demo: {ViewModel.TotalPackages} pacchi, " +
            $"{ViewModel.TotalTheoreticalCubicMeters:N3} m³ utili teorici.\n" +
            $"I {physicalPackages.Count} pacchi fisici sono stati generati in memoria dai gruppi.\n\n" +
            "Nessun dato è stato salvato e nessuna etichetta è stata stampata.",
            "Funzione dimostrativa", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
