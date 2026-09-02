using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.ViewModels;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.Views;

public partial class ConsumablesView : UserControl
{
    private ConsumablesViewModel ViewModel => (ConsumablesViewModel)DataContext;
    public ConsumablesView() { InitializeComponent(); DataContext = new ConsumablesViewModel(); }
    private void ConfirmInventory_Click(object sender, RoutedEventArgs e)
    {
        try { var count = ViewModel.ConfirmInventory(); MessageBox.Show($"Inventario confermato. Rilevazioni registrate: {count}.", "Materiali di consumo", MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Inventario non valido", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void SaveOrders_Click(object sender, RoutedEventArgs e) { ViewModel.SaveOrders(); MessageBox.Show("Informazioni ordine aggiornate in memoria.", "Materiali di consumo", MessageBoxButton.OK, MessageBoxImage.Information); }
    private void PrintInventorySheet_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            new ConsumableInventoryPrintService().Print(Window.GetWindow(this),
                ViewModel.InventoryRows.Select(row => row.Item), ViewModel.InventoryDate, ViewModel.SelectedOperator);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Stampa scheda inventario", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
