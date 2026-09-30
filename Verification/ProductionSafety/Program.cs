using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MagazzinoLegname;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.ViewModels;
using MagazzinoLegname.Views;
using Microsoft.EntityFrameworkCore;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            using var db = SqlPersistenceRoot.ContextFactory.CreateDbContext();
            if (db.Database.GetPendingMigrations().Any()) throw new InvalidOperationException("Test interrotto: migration pendenti, nessuna applicazione autorizzata.");
            if (!db.ApplicationSettings.Any() || !db.ThicknessFamilies.Any()) throw new InvalidOperationException("Test richiede configurazioni esistenti: nessuna inizializzazione autorizzata.");
            var before = Snapshot();
            var app = new App(); app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Exception? failure = null;
            app.Startup += (_, _) => app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    var view = new SettingsView();
                    var window = new Window { Content = view, Width = 1100, Height = 700, ShowInTaskbar = false, Title = "Verifica impostazioni produzione" };
                    window.Show();
                    foreach (var section in new[] { "Consumables", "LegacyImport" })
                    {
                        ((SettingsViewModel)view.DataContext).SelectedSection = section;
                        window.UpdateLayout();
                        var labels = Descendants(view).OfType<Button>().Select(x => x.Content?.ToString() ?? "").ToArray();
                        Require(!labels.Any(x => x.Contains("demo", StringComparison.OrdinalIgnoreCase) || x.Contains("test", StringComparison.OrdinalIgnoreCase)), "B: nessun reset nella UI compilata");
                        if (section == "Consumables") Require(!labels.Any(x => x.Contains("Excel storico", StringComparison.OrdinalIgnoreCase)), "A: nessun import storico consumabili");
                    }
                    Require(!typeof(SettingsViewModel).GetMethods().Any(x => x.Name is "ResetOperationalTestData" or "ImportConsumablesLegacy" or "AnalyzeConsumablesLegacy"), "Comandi rimossi dal ViewModel");
#if DEBUG
                    Require(typeof(App).Assembly.GetType("MagazzinoLegname.Services.InMemoryTestDataResetService") != null, "Servizio tecnico presente solo Debug");
                    MagazzinoLegname.Services.InMemoryTestDataResetService.Shared.ResetOperationalData();
                    Require(before == Snapshot(), "D: reset Debug senza modifiche SQL");
#else
                    Require(typeof(App).Assembly.GetType("MagazzinoLegname.Services.InMemoryTestDataResetService") == null, "B: servizio reset assente dal binario Release");
#endif
                    Require(before == Snapshot(), "C: avvio e Impostazioni non modificano i dati SQL");
                    window.Close();
                }
                catch (Exception ex) { failure = ex; }
                finally { app.Shutdown(); }
            }));
            app.Run();
            if (failure != null) throw failure;
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static string Snapshot()
    {
        using var db = SqlPersistenceRoot.ContextFactory.CreateDbContext();
        var connection = db.Database.GetDbConnection(); connection.Open();
        using var tables = connection.CreateCommand();
        tables.CommandText = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_SCHEMA,TABLE_NAME";
        var names = new List<string>();
        using (var reader = tables.ExecuteReader()) while(reader.Read()) names.Add("[" + reader.GetString(0).Replace("]", "]]") + "].[" + reader.GetString(1).Replace("]", "]]") + "]");
        var rows = new List<string>();
        foreach(var name in names)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT_BIG(*), COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)),0) FROM " + name;
            using var reader = command.ExecuteReader(); reader.Read();
            rows.Add(name + ":" + reader.GetInt64(0) + ":" + reader.GetInt32(1));
        }
        return string.Join("\n", rows);
    }
}
