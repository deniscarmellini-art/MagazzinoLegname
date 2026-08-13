using System.Windows.Controls;
using System.Windows;
using MagazzinoLegname.Models;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class InventoryView : UserControl
{
    private InventoryViewModel ViewModel => (InventoryViewModel)DataContext;
    public InventoryView()
    {
        InitializeComponent();
        DataContext = new InventoryViewModel();
    }

    private void DeletePackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InventoryPackage package }) return;
        var result = MessageBox.Show(
            $"Eliminare il pacco {package.PackageCode} dalla disponibilità locale?\n\nL'operazione non viene salvata su database.",
            "Elimina pacco", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes) ViewModel.RemovePackage(package);
    }
}
