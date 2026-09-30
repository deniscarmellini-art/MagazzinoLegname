using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.Navigation;
using MagazzinoLegname.ViewModels;
namespace MagazzinoLegname.Views;
public partial class PlanningView : UserControl, INavigationAware
{
    private PlanningViewModel ViewModel => (PlanningViewModel)DataContext;
    public PlanningView()
    {
        InitializeComponent(); DataContext = new PlanningViewModel();
#if DEBUG
        LayoutUpdated += (_, _) => MagazzinoLegname.Services.PlanningDiagnostics.Log("12 UI update completed (LayoutUpdated)");
#endif
        ViewModel.ErrorOccurred += message => MessageBox.Show(message, "Pianificazione SQL", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    public void OnNavigatedTo() => ViewModel.Refresh();
    private void Reload_Click(object sender, RoutedEventArgs e) => ViewModel.Refresh();
}
