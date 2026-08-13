using System.Windows;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class QrPackagePreviewWindow : Window
{
    private readonly PackageLabelPrintService _printService = new();
    private QrPackagePreviewViewModel ViewModel => (QrPackagePreviewViewModel)DataContext;
    public QrPackagePreviewWindow(IEnumerable<PhysicalPackageDraft> packages, string supplierName,
        string loadNumber, string deliveryNoteNumber, string certification)
    {
        InitializeComponent();
        DataContext = new QrPackagePreviewViewModel(packages, supplierName, loadNumber, deliveryNoteNumber, certification);
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void PrintCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentLabel is not null)
            _printService.Print([ViewModel.CurrentLabel], $"Etichetta {ViewModel.CurrentLabel.Package.PackageCode}");
    }
    private void PrintAll_Click(object sender, RoutedEventArgs e) =>
        _printService.Print(ViewModel.Labels, $"Etichette carico {ViewModel.CurrentLabel?.LoadNumber}");
}
