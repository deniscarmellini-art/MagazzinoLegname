using System.Windows;
using System.Windows.Controls;
using MagazzinoLegname.ViewModels;
using Microsoft.Win32;

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
    private void MaterialParametersSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowMaterialParameters();
    private void PlanningParametersSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowPlanningParameters();
    private void OperatorsSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowOperators();
    private void LegacyImportSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowLegacyImport();
    private void SelectLegacyFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Seleziona storico Excel", Filter = "Cartella di lavoro Excel con macro (*.xlsm)|*.xlsm", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog() == true) ViewModel.LegacyFilePath = dialog.FileName;
    }
    private async void AnalyzeLegacy_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.AnalyzeLegacyAsync();
        if (!string.IsNullOrWhiteSpace(ViewModel.LegacyAnalysisError)) MessageBox.Show(ViewModel.LegacyAnalysisError, "Analisi storico Excel", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void ExportLegacyReport_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LegacyReport is null) return;
        var dialog = new SaveFileDialog { Title = "Esporta verifiche migrazione", Filter = "File CSV (*.csv)|*.csv", FileName = "verifica_storico.csv", AddExtension = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var files = new Services.LegacyReportCsvExporter().Export(ViewModel.LegacyReport, dialog.FileName);
            MessageBox.Show($"Creati {files.Count} file CSV nella cartella selezionata.", "Esportazione completata", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Esportazione non riuscita", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void ImportLegacyInMemory_Click(object sender, RoutedEventArgs e)
    {
        var plan = ViewModel.LegacyImportPlan;
        if (plan is null) return;
        var message = $"TEST TEMPORANEO IN MEMORIA\n\n" +
            $"Pacchi: {plan.PackageCount:N0}\nCarichi: {plan.LoadCount:N0}\nClassificati: {plan.ClassifiedCount:N0}\nDa classificare: {plan.ToClassifyCount:N0}\n" +
            $"Gruppi materiale: {plan.MaterialGroupCount:N0}\nGruppi classificati da rettificare: {plan.ClassifiedMaterialGroups:N0}\nGruppi da classificare: {plan.MaterialGroupsToClassify:N0}\n" +
            $"MC fisici: {plan.PhysicalCubicMeters:N5}\nMC disponibili legacy: {plan.LegacyAvailableCubicMeters:N5}\nPrezzi mancanti: {plan.MissingPrices:N0}\n" +
            $"Fingerprint: {plan.FileFingerprint}\n\nI dati saranno persi alla chiusura dell'applicazione. Procedere?";
        if (MessageBox.Show(message, "Conferma importazione giacenza iniziale", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            var result = ViewModel.ImportLegacyInMemory(null);
            MessageBox.Show($"Importazione temporanea completata.\nPacchi: {result.PackagesCreated:N0}\nCarichi: {result.LoadsCreated:N0}\nBatch: {result.Batch.Id}", "Importazione giacenza iniziale", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Importazione bloccata", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void ResetTestData_Click(object sender, RoutedEventArgs e)
    {
        const string message = "Questa operazione elimina i dati operativi TEMPORANEI utilizzati per i test.\nContinuare?";
        if (MessageBox.Show(message, "Azzera dati demo/test", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        ViewModel.ResetOperationalTestData();
        MessageBox.Show("Dati operativi temporanei azzerati. Configurazioni, fornitori, parametri e operatori non sono stati modificati.",
            "Store in-memory vuoto", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void AddContact_Click(object sender, RoutedEventArgs e) => ViewModel.AddContact();
    private void DeleteContact_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteContact();
    private void AddOperator_Click(object sender, RoutedEventArgs e) => ViewModel.AddOperator();
    private void EditOperator_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Models.Operator item }) return;
        OperatorsGrid.SelectedItem = item;
        OperatorsGrid.CurrentCell = new DataGridCellInfo(item, OperatorsGrid.Columns[0]);
        OperatorsGrid.BeginEdit();
    }
    private void ToggleOperator_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Models.Operator item }) ViewModel.ToggleOperator(item);
    }
    private void SaveMaterialParameters_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveMaterialParameters();
        MessageBox.Show("Parametri materiale aggiornati in memoria.", "Parametri materiale", MessageBoxButton.OK, MessageBoxImage.Information);
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
