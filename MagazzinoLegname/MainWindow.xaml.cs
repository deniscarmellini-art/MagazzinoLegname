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
        ApplyHeader(PageKey.Dashboard);
        _navigationService.NavigateTo(PageKey.Dashboard);
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Content: string pageLabel }
            && TryGetPageKey(pageLabel, out var pageKey))
        {
            ApplyHeader(pageKey);
            _navigationService.NavigateTo(pageKey);
        }
    }

    public void NavigateToHistory()
    {
        HistoryNavigationButton.IsChecked = true;
        ApplyHeader(PageKey.History);
        _navigationService.NavigateTo(PageKey.History);
    }

    private void ApplyHeader(PageKey pageKey)
    {
        (HeaderTitle.Text, HeaderDescription.Text) = pageKey switch
        {
            PageKey.Dashboard => ("Magazzino Legname", "Gestione materiali, giacenze e movimentazioni"),
            PageKey.GoodsReceipt => ("Entrata merce", "Registrazione dei carichi in ingresso e preparazione delle etichette pacco."),
            PageKey.Classification => ("Classificazione", "Classificazione dei gruppi omogenei ricevuti e assegnazione dell'operatore."),
            PageKey.WasteCorrection => ("Rettifica scarti", "Consuntivo per gruppo materiale classificato; verifica degli scarti e aggiornamento della giacenza reale."),
            PageKey.MaterialDispatch => ("Scarico materiale", "Lettura e verifica dei pacchi da rimuovere dalla disponibilità di magazzino."),
            PageKey.Inventory => ("Disponibilità Magazzino", "Pacchi fisicamente presenti, quantità consolidate e giacenza disponibile."),
            PageKey.Planning => ("Pianificazione", "Arrivi previsti e proiezione settimanale delle giacenze."),
            PageKey.History => ("Storico", "Registro permanente di entrate, classificazioni, rettifiche, scarichi e rimozioni manuali."),
            PageKey.Statistics => ("Statistiche", "Analisi storica di acquisti, scarti, rendimento, consumi e movimentazioni."),
            PageKey.Settings => ("Impostazioni", "Anagrafiche e configurazioni operative dell'applicazione."),
            _ => ("Magazzino Legname", "Gestione materiali, giacenze e movimentazioni")
        };
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
            "Statistiche" => PageKey.Statistics,
            "Impostazioni" => PageKey.Settings,
            _ => default
        };
        return label is "Dashboard" or "Entrata merce" or "Classificazione" or "Rettifica scarti"
            or "Scarico materiale" or "Giacenze" or "Pianificazione" or "Storico" or "Statistiche" or "Impostazioni";
    }
}
