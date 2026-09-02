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
    private void ConsumablesSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowConsumables();
    private void LegacyImportSection_Click(object sender, RoutedEventArgs e) => ViewModel.ShowLegacyImport();
    private void AddConsumable_Click(object sender, RoutedEventArgs e) => ViewModel.AddConsumable();
    private void SaveConsumable_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.SaveConsumables(); MessageBox.Show("Articolo aggiornato in memoria.", "Materiali di consumo", MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Anagrafica non valida", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void ToggleConsumable_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleConsumable();
    private void EditConsumable_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: Models.ConsumableItem item }) ViewModel.SelectConsumable(item); }
    private void ToggleConsumableRow_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: Models.ConsumableItem item }) { ViewModel.SelectConsumable(item); ViewModel.ToggleConsumable(); } }
    private void SelectConsumablePhoto_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedConsumable is null) return;
        var dialog = new OpenFileDialog { Title = "Seleziona foto articolo", Filter = "Immagini|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true };
        if (dialog.ShowDialog() == true) ViewModel.SelectedConsumable.PhotoPath = dialog.FileName;
    }
    private void RemoveConsumablePhoto_Click(object sender, RoutedEventArgs e) { if (ViewModel.SelectedConsumable is not null) ViewModel.SelectedConsumable.PhotoPath = null; }
    private void SelectConsumablesLegacy_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Seleziona Materiale di Consumo CLT-XLAM", Filter = "File Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm", CheckFileExists = true };
        if (dialog.ShowDialog() == true) ViewModel.ConsumablesLegacyFilePath = dialog.FileName;
    }
    private void AnalyzeConsumablesLegacy_Click(object sender, RoutedEventArgs e)
    {
        try { ViewModel.AnalyzeConsumablesLegacy(); }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Analisi consumabili", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void ImportConsumablesLegacy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = ViewModel.ImportConsumablesLegacy();
            MessageBox.Show($"Importazione completata.\n\nArticoli creati: {result.Items:N0}\nRilevazioni create: {result.Readings:N0}\nPeriodo: {result.FirstReading:dd/MM/yyyy} - {result.LastReading:dd/MM/yyyy}\nArticoli da verificare: {result.ItemsToVerify:N0}\nFoto associate: {result.Photos:N0}\nFoto non associate: {result.UnassociatedPhotos:N0}", "Materiali di consumo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Importazione consumabili", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
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
    private void ImportClosedHistoryInMemory_Click(object sender, RoutedEventArgs e)
    {
        var plan = ViewModel.LegacyClosedHistoryPlan;
        if (plan is null) return;
        var message = $"STORICO CHIUSO · STORE TEMPORANEO IN MEMORIA\n\n" +
            $"Record: {plan.RecordCount:N0}\nCarichi distinti: {plan.DistinctLoads:N0}\nFornitori: {plan.DistinctSuppliers:N0}\n" +
            $"Periodo: {plan.CoveredPeriod}\nMC fisici: {plan.PhysicalCubicMeters:N5}\nRighe con anomalie informative: {plan.RowsWithWarnings:N0}\n" +
            $"Fingerprint: {plan.FileFingerprint}\n\nI record saranno aggiunti esclusivamente allo storico legacy e non alla giacenza. Procedere?";
        if (MessageBox.Show(message, "Conferma importazione storico chiuso", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            var result = ViewModel.ImportClosedHistoryInMemory();
            MessageBox.Show($"Storico chiuso importato.\nRecord: {result.ImportedRecords:N0}\nCarichi: {result.DistinctLoads:N0}\nBatch: {result.Batch.Id}",
                "Importazione storico chiuso", MessageBoxButton.OK, MessageBoxImage.Information);
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
