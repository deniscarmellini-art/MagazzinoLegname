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
        var dialog = new ManualPackageRemovalWindow(package.PackageCode)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
            ViewModel.RemovePackage(package, dialog.OperatorName, dialog.Reason, dialog.Note);
    }
}
