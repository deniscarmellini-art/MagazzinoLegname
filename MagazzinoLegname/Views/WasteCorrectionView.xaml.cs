using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.Navigation;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class WasteCorrectionView : UserControl, INavigationAware
{
    private WasteCorrectionViewModel ViewModel => (WasteCorrectionViewModel)DataContext;
    public WasteCorrectionView()
    {
        InitializeComponent();
        DataContext = new WasteCorrectionViewModel();
    }

    private void PartialWastePercentage_LostFocus(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedGroup?.CommitPartialWastePercentageText();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ViewModel.ConfirmSelected()) return;
            MessageBox.Show("Rettifica registrata. Il gruppo è ora disponibile.",
                "Rettifica scarti", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(exception.Message, "Rettifica scarti", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void OnNavigatedTo()
    {
        try { ViewModel.ReloadFromDatabase(); }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(exception.Message, "Database non disponibile", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
