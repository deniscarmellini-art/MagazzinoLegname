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
        if (sender is RadioButton { Content: string pageLabel }
            && TryGetPageKey(pageLabel, out var pageKey))
        {
            _navigationService.NavigateTo(pageKey);
        }
    }

    private static bool TryGetPageKey(string label, out PageKey pageKey)
    {
        pageKey = label switch
        {
            "Dashboard" => PageKey.Dashboard,
            "Entrata merce" => PageKey.GoodsReceipt,
            "Classificazione" => PageKey.Classification,
            "Rettifica scarti" => PageKey.WasteCorrection,
            "Scarico materiale" => PageKey.MaterialDispatch,
            "Giacenze" => PageKey.Inventory,
            "Pianificazione" => PageKey.Planning,
            "Storico" => PageKey.History,
            "Impostazioni" => PageKey.Settings,
            _ => default
        };
        return label is "Dashboard" or "Entrata merce" or "Classificazione" or "Rettifica scarti"
            or "Scarico materiale" or "Giacenze" or "Pianificazione" or "Storico" or "Impostazioni";
    }
}
