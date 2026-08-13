using System.Windows;
using MagazzinoLegname.Models;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class QrPackagePreviewWindow : Window
{
    public QrPackagePreviewWindow(IEnumerable<PhysicalPackageDraft> packages,
        string supplierName, string loadNumber)
    {
        InitializeComponent();
        DataContext = new QrPackagePreviewViewModel(packages, supplierName, loadNumber);
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
