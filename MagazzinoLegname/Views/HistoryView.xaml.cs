using System.Windows.Controls;

using System.Windows;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class HistoryView : UserControl
{
    private HistoryViewModel ViewModel => (HistoryViewModel)DataContext;

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
