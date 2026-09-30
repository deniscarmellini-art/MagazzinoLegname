using System.Windows;
using MagazzinoLegname.Navigation;
using System.Windows.Controls;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class StatisticsView : UserControl, INavigationAware
{
    private StatisticsViewModel ViewModel => (StatisticsViewModel)DataContext;
    public void OnNavigatedTo() => ViewModel.Refresh();

    private void RefreshSql_Click(object sender, RoutedEventArgs e) => ViewModel.Refresh();

    public StatisticsView()
    {
        InitializeComponent();
        DataContext = new StatisticsViewModel();
    }
}
