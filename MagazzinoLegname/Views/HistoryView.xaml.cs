using MagazzinoLegname.Navigation;
using System.Windows.Controls;

using System.Windows;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class HistoryView : UserControl, INavigationAware
{
    private HistoryViewModel ViewModel => (HistoryViewModel)DataContext;

    public void OnNavigatedTo() => ViewModel.Refresh();

    private void RefreshSql_Click(object sender, RoutedEventArgs e) => ViewModel.Refresh();

    public HistoryView()
    {
        InitializeComponent();
        DataContext = new HistoryViewModel();
    }

    private void QuickFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string filter }) ViewModel.QuickFilter = filter;
    }

    private void LoadHistory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid loadId }) ViewModel.SelectLoad(loadId);
    }

    private void CloseLoadHistory_Click(object sender, RoutedEventArgs e) => ViewModel.CloseLoadHistory();
}
