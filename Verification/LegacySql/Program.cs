using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Entities;
using MagazzinoLegname.Persistence.Repositories;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--verify-restart") { VerifyRestart(args[1]); return 0; }
        var (original, _) = DatabaseSettingsLoader.Load();
        var database = "MagazzinoLegname_TestLegacy_" + Guid.NewGuid().ToString("N");
        var folder = Path.Combine(AppContext.BaseDirectory, "fixture-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        Directory.CreateDirectory(folder);
        original.Database = database;
        var config = Path.Combine(folder, "database.settings.json");
        File.WriteAllText(config, JsonSerializer.Serialize(new DatabaseSettingsDocument { Database = original }));
        Environment.SetEnvironmentVariable(DatabaseSettingsLoader.SettingsPathEnvironmentVariable, config);
        var factory = new MagazzinoDbContextFactory();
        try
        {
            using (var db = factory.CreateDbContext()) db.Database.Migrate();
            SqlPersistenceRoot.DomainConfigurations.LoadOrInitialize();
            var path = Path.Combine(folder, "fixture.xlsm");
            var rows = FixtureRows("1-24", Random.Shared.Next(7, 12));
            WriteWorkbook(path, rows);
            var importer = new LegacySqlImportService(factory);
            var preview = importer.Analyze(path);
            Console.WriteLine(preview.FileIdentity + "\n" + preview.Summary);
            Check(preview.CanImport, "preview valida: " + string.Join(";", preview.BlockingErrors));
            // Deliberately conflicting supplier list price must never enter historical valuation.
            using (var db = factory.CreateDbContext())
            {
                var supplier = new SupplierEntity { Id = Guid.NewGuid(), Name = "Fixture Legname", Code = "FIXTURE", IsActive = true };
                db.Suppliers.Add(supplier);
                db.SupplierPrices.Add(new SupplierPriceEntity { Id = Guid.NewGuid(), SupplierId = supplier.Id, ConventionalThickness = 23, PricePerCubicMeter = 9999, ValidFrom = new DateTime(2020,1,1) });
                db.SaveChanges();
            }
            var result = importer.Import(preview, "Operatore fixture");
            Reconcile(factory, preview);
            Check(result.ImportedPackages == preview.Report.CurrentInventoryRows && result.ImportedHistory == preview.Report.ClosedHistoryRows, "A import completo e I quadratura workbook/SQL");
            var before = Snapshot(factory);
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("--verify-restart"); start.ArgumentList.Add(path);
            using(var child = Process.Start(start)!)
            {
                var output = child.StandardOutput.ReadToEnd(); var error = child.StandardError.ReadToEnd(); child.WaitForExit();
                Console.Write(output); Check(child.ExitCode == 0, "B nuovo processo: " + error);
            }
            Check(importer.Import(importer.Analyze(path), "Fixture").ImportedPackages == 0 && Snapshot(factory) == before, "C reimport identico senza duplicati");
            // Different fingerprint, same source rows, different order: no duplicate keys across files.
            var second = Path.Combine(folder, "riordinato.xlsm"); WriteWorkbook(second, rows.AsEnumerable().Reverse().ToList());
            var secondPreview = importer.Analyze(second);
            Check(secondPreview.Fingerprint != preview.Fingerprint && secondPreview.KnownRows == rows.Count, "chiavi sorgente indipendenti da file e posizione riga");
            Check(importer.Import(secondPreview, "Fixture").ImportedHistory == 0 && Snapshot(factory) == before, "C file diverso con righe note: nessun duplicato");
            var changed = Path.Combine(folder, "modificato.xlsm"); var changedRows = FixtureRows("2-24", 5); WriteWorkbook(changed, changedRows);
            var stale = importer.Analyze(changed); changedRows[0] = changedRows[0] with { Price = 555 }; WriteWorkbook(changed, changedRows);
            Reject(() => importer.Import(stale, "Fixture"), "D file cambiato: nuova analisi obbligatoria");
            Check(Snapshot(factory) == before, "D nessuna scrittura");
            var failFactory = new FaultFactory(original.BuildConnectionString());
            var failImporter = new LegacySqlImportService(failFactory);
            Reject(() => failImporter.Import(failImporter.Analyze(changed), "Fixture"), "E errore forzato dopo batch SQL");
            Check(failFactory.Interceptor.Fired && Snapshot(factory) == before, "E rollback integrale: nessun batch, chiave, carico o storico parziale");
            var missing = Path.Combine(folder, "senza-prezzo.xlsm"); var badRows = FixtureRows("3-24", 4); badRows[0] = badRows[0] with { Price = null }; WriteWorkbook(missing,badRows);
            Check(!importer.Analyze(missing).CanImport, "Prezzo mancante bloccante");
            badRows = FixtureRows("4-24", 4); badRows.Add(badRows[0]); WriteWorkbook(missing,badRows);
            Check(!importer.Analyze(missing).CanImport, "Duplicato ambiguo bloccante");
            var app = new MagazzinoLegname.App(); app.InitializeComponent();
            var dashboard = new DashboardViewModel(); dashboard.Refresh(); Check(dashboard.IsDataAvailable, "F Dashboard SQL");
            var inventory = new InventoryViewModel(); inventory.Refresh(); Check(inventory.IsDataAvailable, "F Giacenze SQL");
            var history = new HistoryViewModel(); history.Refresh(); Check(history.IsDataAvailable, "F Storico SQL");
            var historicalId = LegacyHistoricalStore.Shared.Records.First().HistoricalLoadId;
            history.SelectLoad(historicalId);
            Check(history.SelectedLoadSummary?.DischargedPackages == preview.Report.Rows.Count(x => x.FinishedOn.HasValue && !LegacyMovementClassifier.IsSupplierReturn(x.FinishedRawValue)), "F riepilogo storico non confonde scarichi, resi e Scarto");
            var statistics = new StatisticsViewModel(); statistics.DateFrom = null; statistics.DateTo = null; statistics.Refresh();
            Check(statistics.IsDataAvailable, "F quattro pagine SQL disponibili");
            Check(dashboard.InventoryValue == preview.Report.CurrentImportableValue && inventory.InventoryValue == preview.Report.CurrentImportableValue, "F/G valore storico Dashboard e Giacenze");
            Check(dashboard.RealCubicMeters == 0 && dashboard.CubicMetersToConsolidate == preview.Report.CurrentPhysicalCubicMeters, "F MC da consolidare senza rettifiche inventate");
            Check(LegacyHistoricalStore.Shared.Records.Count == preview.Report.ClosedHistoryRows, "F storico SQL ricaricato");
            Check(LegacyHistoricalStore.Shared.Records.Single(x => x.FinishedRawValue == "Scarto").MovementType == "Chiusura legacy", "Scarto non reinterpretato");
            Check(statistics.CubicMetersReturned == preview.Report.Rows.Where(x => LegacyMovementClassifier.IsSupplierReturn(x.FinishedRawValue)).Sum(x=>x.RecalculatedPhysicalCubicMeters ?? 0), "F resi storici a volume fisico");
            var workflow = ClassificationWorkflowService.Shared;
            Check(workflow.Loads.SelectMany(x=>x.Groups).Where(x=>!x.IsClassified).Sum(x=>x.PackageCount) == preview.Report.ToClassify, "H pacchi realmente disponibili da classificare");
            Check(workflow.Loads.SelectMany(x=>x.Groups).Where(x=>x.IsClassified).Sum(x=>x.PackageCount) == preview.Report.Classified, "H classificati SQL coerenti");
            Check(Snapshot(factory) == before, "Consultazione senza scritture");
            Console.WriteLine("ALL A-I PASS. Fixture: " + path);
            File.WriteAllText(Path.Combine(folder,"esito.txt"), preview.FileIdentity + "\n" + preview.Summary + "\nTest A-I PASS; database fixture eliminato a fine test.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            // Only the unique database created by this process can be removed.
            if (!database.StartsWith("MagazzinoLegname_TestLegacy_", StringComparison.Ordinal) || database.Length != "MagazzinoLegname_TestLegacy_".Length + 32) throw new InvalidOperationException("Cleanup guard");
            using var cleanup = factory.CreateDbContext();
            if (cleanup.Database.GetDbConnection().Database != database) throw new InvalidOperationException("Cleanup target mismatch");
            cleanup.Database.EnsureDeleted();
        }
    }
    private static void VerifyRestart(string path)
    {
        var factory = new MagazzinoDbContextFactory();
        var importer = new LegacySqlImportService(factory); var preview = importer.Analyze(path);
        Reconcile(factory, preview); LegacyHistoricalStore.Shared.Reload();
        Check(LegacyHistoricalStore.Shared.Records.Count == preview.Report.ClosedHistoryRows, "B storico SQL dopo riavvio processo");
    }
    private static void Reconcile(MagazzinoDbContextFactory factory, LegacySqlPreview p)
    {
        using var db = factory.CreateDbContext();
        var packages = db.Packages.AsNoTracking().ToArray(); var historical = db.LegacyHistoricalRecords.AsNoTracking().ToArray();
        var current = p.Report.Rows.Where(x=>x.Category == MagazzinoLegname.Models.LegacyRowCategory.InitialInventory).ToArray();
        Check(packages.Length == current.Length && db.Loads.Count() == p.Report.CurrentLoads, "I numero carichi/pacchi");
        Check(packages.Sum(x=>x.IncomingPhysicalCubicMeters) == current.Sum(x=>decimal.Round(x.Pieces!.Value*x.InputThickness!.Value*x.InputWidth!.Value*x.InputLength!.Value/1000000000m,9,MidpointRounding.AwayFromZero)), "I MC fisici senza riduzioni");
        Check(packages.Sum(x=>x.HistoricalPackageValue ?? 0) == current.Sum(x=>decimal.Round(x.RecalculatedPhysicalCubicMeters!.Value*x.AppliedPrice!.Value,4,MidpointRounding.AwayFromZero)), "G/I valore storico SQL");
        foreach(var package in packages) Check(package.AppliedPrice == current.Single(x=>x.ExcelRow==package.LegacyExcelRow).AppliedPrice, "G prezzo pacco da workbook");
        Check(historical.Length == p.Report.ClosedHistoryRows && db.LegacyImportKeys.Count() == p.Report.TotalRows, "I storico e chiavi");
        Check(historical.Count(x=>x.FinishedOn.HasValue) == p.Report.Rows.Count(x=>x.FinishedOn.HasValue), "I scarichi datati");
        Check(historical.Count(x=>LegacyMovementClassifier.IsSupplierReturn(x.FinishedRawValue)) == p.Report.Rows.Count(x=>LegacyMovementClassifier.IsSupplierReturn(x.FinishedRawValue)), "I resi");
        Check(db.MaterialGroups.Where(x=>x.IsClassified).Sum(x=>x.PackageCount) == p.Report.Classified && !db.WasteAdjustments.Any() && !db.ClassificationMovements.Any(), "H stato classificato, nessuna rettifica o operatore inventato");
    }
    private static string Snapshot(MagazzinoDbContextFactory f)
    {
        using var db = f.CreateDbContext();
        return string.Join("|", db.LegacyImportBatches.Count(),db.LegacyImportKeys.Count(),db.Loads.Count(),db.MaterialGroups.Count(),db.Packages.Count(),db.LegacyHistoricalRecords.Count(),db.Suppliers.Count(),db.Packages.Sum(x=>(decimal?)x.HistoricalPackageValue) ?? 0);
    }
    private static void Check(bool ok, string name) { if(!ok) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
    private static void Reject(Action action, string name) { try { action(); } catch(Exception ex) when (ex is InvalidOperationException || ex is DbUpdateException { InnerException: InvalidOperationException }) { Console.WriteLine("PASS " + name); return; } throw new Exception("FAIL " + name); }
    private sealed record FixtureRow(string Load, int Number, int Pieces, decimal? Price, bool Classified, string Finished);
    private static List<FixtureRow> FixtureRows(string load, int count) => Enumerable.Range(1,count).Select(i => new FixtureRow(load,i,20+i,300+i*10,i%2==0,i<=count-3 ? "" : i==count-2 ? "15/02/2024" : i==count-1 ? "Reso" : "Scarto")).ToList();
    private static void WriteWorkbook(string path, List<FixtureRow> rows)
    {
        // Open XML .xlsm fixture matching the actual reader, with no VBA execution or external links.
        using var doc = SpreadsheetDocument.Create(path,SpreadsheetDocumentType.MacroEnabledWorkbook);
        var wb = doc.AddWorkbookPart(); wb.Workbook = new Workbook(); var sheets = wb.Workbook.AppendChild(new Sheets());
        var headers = new[] {"Fornitore","Data","Carico","Numero Etichetta","Pezzi","A","B","C","Finito il","Qualità","Classificato","Prezzo"};
        void Sheet(string name, IEnumerable<FixtureRow> source)
        {
            var part = wb.AddNewPart<WorksheetPart>(); var data = new SheetData(); part.Worksheet = new Worksheet(data);
            void RowOf(uint index, IEnumerable<string> values)
            {
                var row = new Row { RowIndex = index }; var column=0;
                foreach(var value in values) row.Append(new Cell { CellReference = ((char)('A'+column++)).ToString()+index, DataType=CellValues.InlineString, InlineString=new InlineString(new Text(value)) });
                data.Append(row);
            }
            RowOf(1,headers); uint number=2;
            foreach(var row in source) RowOf(number++,new[] {"Fixture Legname","01/02/2024",row.Load,$"Pacco {row.Number} di {rows.Count}",row.Pieces.ToString(CultureInfo.InvariantCulture),"100","23","4000",row.Finished,"C",row.Classified?"Sì":"No",row.Price?.ToString(CultureInfo.InvariantCulture)??""});
            sheets.Append(new Sheet { Id=wb.GetIdOfPart(part), SheetId=(uint)sheets.Count()+1, Name=name }); part.Worksheet.Save();
        }
        Sheet("Magazzino",rows); Sheet("Materiale Disponibile",rows.Where(x=>x.Finished=="")); wb.Workbook.Save();
    }
    private sealed class FaultFactory(string connection) : IDbContextFactory<MagazzinoDbContext>
    {
        public FailInsert Interceptor {get;} = new();
        public MagazzinoDbContext CreateDbContext() => new(new DbContextOptionsBuilder<MagazzinoDbContext>().UseSqlServer(connection).AddInterceptors(Interceptor).Options);
    }
    private sealed class FailInsert : DbCommandInterceptor
    {
        public bool Fired {get;private set;}
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            if(command.CommandText.Contains("INSERT INTO [LegacyImportKeys]",StringComparison.Ordinal)) { Fired=true; throw new InvalidOperationException("Errore fixture dopo inserimento batch"); }
            return result;
        }
    }
}
