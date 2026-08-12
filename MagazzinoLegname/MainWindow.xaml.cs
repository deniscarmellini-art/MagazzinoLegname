using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.Navigation;

namespace MagazzinoLegname;

public partial class MainWindow : Window
{
    private readonly NavigationService _navigationService = new();

    public MainWindow()
    {
        InitializeComponent();
        _navigationService.PageChanged += (_, page) => PageContent.Content = page;
        _navigationService.NavigateTo(PageKey.Dashboard);
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string pageName }
            && Enum.TryParse(pageName, out PageKey pageKey))
        {
            _navigationService.NavigateTo(pageKey);
        }
    }
}
