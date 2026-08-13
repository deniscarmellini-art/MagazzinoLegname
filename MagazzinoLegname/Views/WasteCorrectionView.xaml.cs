using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class WasteCorrectionView : UserControl
{
    private WasteCorrectionViewModel ViewModel => (WasteCorrectionViewModel)DataContext;
    public WasteCorrectionView()
    {
        InitializeComponent();
        DataContext = new WasteCorrectionViewModel();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.ConfirmSelected()) return;
        MessageBox.Show("Rettifica registrata in memoria. Il gruppo è ora disponibile.",
            "Rettifica scarti", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
