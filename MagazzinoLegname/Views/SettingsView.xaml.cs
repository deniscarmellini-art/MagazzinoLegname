using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.ViewModels;

namespace MagazzinoLegname.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;
    public SettingsView()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }
    private void AddSupplier_Click(object sender, RoutedEventArgs e) => ViewModel.AddSupplier();
    private void SaveSupplier_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SaveSupplier();
            MessageBox.Show("Anagrafica aggiornata in memoria.", "Fornitori", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Anagrafica non valida", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    private void ToggleHistory_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleHistory();
    private void SuppliersSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowSuppliers();
    private void GeneralSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowGeneral();
    private void MaterialParametersSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowMaterialParameters();
    private void PlanningParametersSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowPlanningParameters();
    private void AddContact_Click(object sender, RoutedEventArgs e) => ViewModel.AddContact();
    private void DeleteContact_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteContact();
    private void SaveMaterialParameters_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveMaterialParameters();
        MessageBox.Show("Parametri materiale aggiornati in memoria.", "Parametri materiale", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void SaveGeneralSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveGeneralSettings();
        MessageBox.Show("Impostazioni generali aggiornate in memoria.", "Impostazioni generali", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void SavePlanningSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SavePlanningSettings();
        MessageBox.Show("Parametri di pianificazione aggiornati in memoria.", "Parametri pianificazione", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void AddPrice_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: SupplierConfigurationRowViewModel editor })
                ViewModel.SelectPriceEditor(editor);
            ViewModel.AddPrice();
            MessageBox.Show("Nuovo periodo prezzo creato. Il periodo precedente è stato chiuso automaticamente.",
                "Listino fornitori", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Prezzo non valido", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
