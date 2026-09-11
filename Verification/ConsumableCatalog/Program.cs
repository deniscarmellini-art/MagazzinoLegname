using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Repositories;
using MagazzinoLegname.Services;
using Microsoft.EntityFrameworkCore;

var (settings, _) = DatabaseSettingsLoader.Load();
if (settings.Database != "MagazzinoLegname_Dev") throw new InvalidOperationException("Tests require MagazzinoLegname_Dev.");
var factory = new MagazzinoDbContextFactory();
var repository = new SqlConsumableRepository(factory);
var id = Guid.Parse(args[1]);
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
ConsumableItem Read() => new SqlConsumableRepository(new MagazzinoDbContextFactory()).Get(id) ?? throw new Exception("Missing test item");
switch (args[0])
{
    case "create":
        using (var db = factory.CreateDbContext())
        {
            Check(!db.Database.HasPendingModelChanges(), "model matches migration snapshot");
            db.Database.Migrate();
        }
        var item = new ConsumableItem { Id = id, InternalCode = $"TEST-{id:N}", ProductName = "TEST SQL", SupplierName = "Fornitore consumabili TEST", Department = "Reparto TEST", UnitOfMeasure = "kg", QuantityPerUnit = 1000m, MinimumStock = 2.25m, ConsumptionAverageQuantity = 0.125m, ConsumptionAverageText = "0,125 al mese", ConsumptionPeriod = ConsumptionPeriod.Month, LeadTimeDays = 4, LeadTimeText = "4 giorni", Packaging = "Confezione TEST", Notes = "Test Fase 2C-1", IsActive = true, PhotoPath = "not-persisted.jpg" };
        repository.Save(item);
        Check(item.RowVersion.Length == 8, "INSERT rowversion");
        break;
    case "update":
        var first = Read();
        Check(first.ProductName == "TEST SQL" && first.QuantityPerUnit == 1000m, "A: persistence across processes");
        Check(first.SupplierName == "Fornitore consumabili TEST" && first.Department == "Reparto TEST" && first.MinimumStock == 2.25m && first.ConsumptionAverageQuantity == 0.125m && first.ConsumptionPeriod == ConsumptionPeriod.Month && first.LeadTimeDays == 4 && first.LeadTimeText == "4 giorni" && first.Packaging == "Confezione TEST" && first.Notes == "Test Fase 2C-1" && first.PhotoPath is null, "base parameters persisted, photo excluded");
        first.QuantityPerUnit = 7000.123456m; first.UnitOfMeasure = "scatola"; first.IsActive = false;
        repository.Save(first);
        break;
    case "verify":
        var second = Read();
        Check(second.QuantityPerUnit == 7000.123456m && second.UnitOfMeasure == "scatola", "B: decimal persists across processes");
        Check(!second.IsActive, "C: inactive persists across processes");
        var stale = Read(); var current = Read();
        current.Notes = "winner"; repository.Save(current);
        stale.Notes = "loser";
        try { repository.Save(stale); throw new Exception("Expected concurrency exception"); }
        catch (DbUpdateConcurrencyException) { Console.WriteLine("PASS D: SQL rejects stale RowVersion"); }
        var service = new ConsumableCatalogService(repository);
        service.Reload();
        try { service.Save(stale); throw new Exception("Expected handled conflict"); }
        catch (ConsumableCatalogRefreshException ex) { Check(ex.Message.Contains("ricaricati da SQL"), "D: clear conflict message"); }
        Check(service.Items.Single(x => x.Id == id).Notes == "winner" && Read().Notes == "winner", "D: reload from SQL without overwrite");
        var draft = service.LoadDetail(id); draft.ProductName = "unsaved";
        Check(service.Items.Single(x => x.Id == id).ProductName == "TEST SQL", "detached editing");
        var invalid = Read(); invalid.QuantityPerUnit = -1;
        try { repository.Save(invalid); throw new Exception("Expected validation"); }
        catch (InvalidOperationException) { Check(Read().QuantityPerUnit == 7000.123456m, "invalid quantity does not persist"); }
        using (var db = factory.CreateDbContext()) db.ConsumableItems.Where(x => x.Id == id).ExecuteDelete();
        try { repository.Save(current); throw new Exception("Expected delete conflict"); }
        catch (DbUpdateConcurrencyException) { Check(repository.Get(id) is null, "deleted item is not reinserted"); }
        break;
    case "cleanup":
        using (var db = factory.CreateDbContext()) db.ConsumableItems.Where(x => x.Id == id && x.InternalCode == $"TEST-{id:N}").ExecuteDelete();
        break;
    default: throw new Exception("Unknown command");
}
