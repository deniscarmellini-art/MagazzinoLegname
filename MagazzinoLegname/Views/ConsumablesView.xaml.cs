using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.ViewModels;
using MagazzinoLegname.Services;
using MagazzinoLegname.Navigation;

namespace MagazzinoLegname.Views;

public partial class ConsumablesView : UserControl, INavigationAware
{
    private ConsumablesViewModel ViewModel => (ConsumablesViewModel)DataContext;
    public ConsumablesView() { InitializeComponent(); DataContext = new ConsumablesViewModel(); }
    public void OnNavigatedTo() => ReloadSql();
    private void ReloadSql_Click(object sender, RoutedEventArgs e) => ReloadSql();
    private void ReloadSql()
    {
        try { ViewModel.Reload(); }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Inventari SQL non disponibili", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void ConfirmInventory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!InventoryGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !InventoryGrid.CommitEdit(DataGridEditingUnit.Row, true))
                throw new InvalidOperationException("Correggere i campi non validi prima di confermare.");
            var count = ViewModel.ConfirmInventory(); MessageBox.Show($"Inventario confermato. Rilevazioni registrate: {count}.", "Materiali di consumo", MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Inventario non valido", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void SaveOrders_Click(object sender, RoutedEventArgs e) { ViewModel.SaveOrders(); MessageBox.Show("Informazioni ordine aggiornate in memoria.", "Materiali di consumo", MessageBoxButton.OK, MessageBoxImage.Information); }
    private void PrintInventorySheet_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            new ConsumableInventoryPrintService().Print(Window.GetWindow(this),
                ViewModel.InventoryRows.Select(row => row.Item), ViewModel.InventoryDate, ViewModel.SelectedOperator?.DisplayName ?? string.Empty);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Stampa scheda inventario", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
