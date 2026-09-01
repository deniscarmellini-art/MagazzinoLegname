using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;
using Microsoft.Win32;

namespace MagazzinoLegname.Views;

public partial class InventoryView : UserControl
{
    private InventoryViewModel ViewModel => (InventoryViewModel)DataContext;

    public InventoryView()
    {
        InitializeComponent();
        DataContext = new InventoryViewModel();
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Esporta giacenze in Excel",
            Filter = "File Excel (*.xlsx)|*.xlsx",
            FileName = $"Giacenze_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            new InventoryExcelExporter().Export(ViewModel.GetVisiblePackagesSnapshot(), dialog.FileName, DateTime.Now);
            MessageBox.Show("Esportazione Excel completata.", "Giacenze", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossibile esportare il file Excel.\n{ex.Message}", "Giacenze", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeletePackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InventoryPackage package }) return;
        var dialog = new ManualPackageRemovalWindow(package.PackageCode, package.IsSupplementary)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
            ViewModel.RemovePackage(package, dialog.OperatorName, dialog.Reason, dialog.Note);
    }
}
