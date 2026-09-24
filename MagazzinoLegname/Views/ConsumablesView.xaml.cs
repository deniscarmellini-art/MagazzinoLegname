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
    private void NewOrder_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.NewOrder(); }
        catch (Exception exception) { ReportOrderError(exception); }
    }
    private void SaveOrders_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ValidateOrderEditor(OrderEditorRoot);
            ViewModel.SaveOrders();
            MessageBox.Show("Ordine salvato e dati ricaricati da SQL. Giacenza inventariale invariata.", "Ordini consumabili", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) { ReportOrderError(exception); }
    }
    private void SituationGrid_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || ItemsControl.ContainerFromElement(SituationGrid, source) is not DataGridRow) return;
        try { ViewModel.OpenOrderEditor(); }
        catch (Exception exception) { ReportOrderError(exception); }
    }
    private void CancelOrder_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.CancelOrder(); }
        catch (Exception exception) { ReportOrderError(exception); }
    }
    private static void ReportOrderError(Exception exception)
    {
        System.Diagnostics.Debug.WriteLine($"[Ordini consumabili] {exception}");
        MessageBox.Show("Operazione ordine non completata. " + exception.Message, "Ordini consumabili", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private static void ValidateOrderEditor(DependencyObject element)
    {
        if (Validation.GetHasError(element)) throw new InvalidOperationException("Correggere i campi non validi prima di salvare l'ordine.");
        if (element is DatePicker picker && !string.IsNullOrWhiteSpace(picker.Text) && !DateTime.TryParse(picker.Text, out _))
            throw new InvalidOperationException("Data ordine o consegna prevista non valida.");
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
            ValidateOrderEditor(System.Windows.Media.VisualTreeHelper.GetChild(element, index));
    }
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
