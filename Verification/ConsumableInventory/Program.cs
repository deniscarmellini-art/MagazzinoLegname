using System.Data.Common;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Entities;
using MagazzinoLegname.Persistence.Repositories;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

static void Check(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL " + label);
    Console.WriteLine("PASS " + label);
}
static void Expect<T>(Action action, string label) where T : Exception
{
    try { action(); }
    catch (T) { Console.WriteLine("PASS " + label); return; }
    throw new Exception("FAIL expected " + typeof(T).Name + ": " + label);
}

if (args[0] == "offline")
{
    var item = new ConsumableItem { QuantityPerUnit = 7000m, MinimumStock = 20000m };
    var row = new ConsumableInventoryEntryRow(item, null) { NewQuantityText = "4" };
    Check(row.CalculatedQuantity == 28000m, "live preview 4 x 7000");
    row.NewQuantityText = "2"; Check(row.CalculatedQuantity == 14000m, "live preview 2 x 7000");
    row.NewQuantityText = "0"; Check(row.CalculatedQuantity == 0m, "zero is a valid count");
    row.NewQuantityText = "-1"; Check(row.NewQuantity is null && row.ValidationMessage.Length > 0, "negative rejected");
    row.NewQuantityText = "1,2,3"; Check(row.NewQuantity is null, "malformed input rejected");
    row.NewQuantityText = "0.000001"; Check(row.CalculatedQuantity == .007m, "six decimals parsed");
    row.NewQuantityText = "0,0000001"; Check(row.CalculatedQuantity is null, "excess precision rejected");
    Check(ConsumableInventoryRules.Calculate(.000001m, .000001m) == .000000000001m, "12 decimal product exact");
    Expect<InvalidOperationException>(() => ConsumableInventoryRules.Calculate(1000000000000m, 1m), "input range");
    Expect<InvalidOperationException>(() => ConsumableInventoryRules.Calculate(100000000m, 100000000m), "product range");
    Check(ConsumableInventoryRules.Status(item, null) == ConsumableStockStatus.ToVerify, "E without reading");
    var reading = new ConsumableSqlReading(Guid.NewGuid(), Guid.NewGuid(), item.Id, DateTime.Today, DateTime.UtcNow,
        "Operatore", "Prodotto", "", "", "scatola", 4m, 7000m, 28000m, "");
    Check(ConsumableInventoryRules.Status(item, reading) == ConsumableStockStatus.Ok, "A and secondary metadata not required");
    Check(ConsumableInventoryRules.Status(item, reading with { CountedUnits = 2, CalculatedQuantity = 14000 }) == ConsumableStockStatus.ToOrder, "B below minimum");
    item.MinimumStock = 28000; Check(ConsumableInventoryRules.Status(item, reading) == ConsumableStockStatus.Ok, "threshold equality");
    item.QuantityPerUnit = null; Check(ConsumableInventoryRules.Status(item, reading) == ConsumableStockStatus.Ok, "historical snapshot supplies package size");
    item.MinimumStock = null; Check(ConsumableInventoryRules.Status(item, reading) == ConsumableStockStatus.ToVerify, "missing minimum");
    var fake = new StubRepository(new([item], [new Operator { FirstName = "Test" }], [reading]));
    var vm = new ConsumablesViewModel(new ConsumableInventoryService(fake)); vm.Reload();
    vm.InventoryRows[0].NewQuantityText = "invalid";
    Expect<InvalidOperationException>(() => vm.ConfirmInventory(), "invalid nonblank row blocks full inventory");
    Check(fake.Saves == 0, "invalid input never reaches persistence");
    fake.FailLoad = true; Expect<InvalidOperationException>(() => vm.Reload(), "SQL load failure is reported");
    Check(!vm.IsSqlAvailable && vm.HistoryRows.Count == 0 && vm.SituationRows.Count == 0 && vm.InventoryRows.Count == 0, "no stale/in-memory fallback");
    return;
}

var (settings, _) = DatabaseSettingsLoader.Load();
if (settings.Database != "MagazzinoLegname_Dev") throw new InvalidOperationException("SQL tests require MagazzinoLegname_Dev.");
var id = Guid.Parse(args[1]);
var code = $"TI-{id:N}"; var emptyCode = $"TN-{id:N}";
var factory = new MagazzinoDbContextFactory();
var catalog = new SqlConsumableRepository(factory);
var repository = new SqlConsumableInventoryRepository(factory);
ConsumableItem Item() => catalog.Get(id) ?? throw new Exception("Missing fixture");
SaveConsumableInventory Request(decimal count, DateTime date) => new(Guid.NewGuid(), date,
    repository.Load().Operators.FirstOrDefault()?.Id ?? throw new Exception("An active SQL operator is required"),
    [new(id, count, Item().RowVersion, "TEST 2C-2")]);

switch (args[0])
{
    case "create":
        using (var db = factory.CreateDbContext())
        {
            Check(!db.Database.HasPendingModelChanges(), "EF model matches migration");
            var pending = db.Database.GetPendingMigrations().ToArray();
            if (pending.Any(x => !x.EndsWith("_AddConsumableInventories", StringComparison.Ordinal)))
                throw new Exception("Unexpected pending migration; not applying unrelated schema changes.");
            db.Database.Migrate();
        }
        catalog.Save(new ConsumableItem { Id = id, InternalCode = code, ProductName = "TEST SQL INVENTARIO", UnitOfMeasure = "scatola", QuantityPerUnit = 7000, MinimumStock = 20000 });
        catalog.Save(new ConsumableItem { InternalCode = emptyCode, ProductName = "TEST SENZA INVENTARIO", UnitOfMeasure = "kg", QuantityPerUnit = 1000, MinimumStock = 10 });
        var request = Request(4, DateTime.Today.AddDays(-1)); repository.Save(request);
        var snapshot = repository.Load(); var initial = snapshot.Latest(id)!;
        Check(initial.CalculatedQuantity == 28000 && initial.CountedUnits == 4 && initial.QuantityPerUnitSnapshot == 7000, "A SQL 4 x 7000 = 28000");
        Check(ConsumableInventoryRules.Status(Item(), initial) == ConsumableStockStatus.Ok, "A SQL status OK");
        var empty = snapshot.Items.Single(x => x.InternalCode == emptyCode);
        Check(ConsumableInventoryRules.Status(empty, snapshot.Latest(empty.Id)) == ConsumableStockStatus.ToVerify, "E SQL no inventory");
        repository.Save(request);
        Check(repository.Load().Readings.Count(x => x.SessionId == request.SessionId) == 1, "idempotent retry no duplicate");
        Expect<DbUpdateConcurrencyException>(() => repository.Save(request with { Readings = [request.Readings[0] with { CountedUnits = 9 }] }), "same session cannot be overwritten");
        break;
    case "update":
        Check(repository.Load().Latest(id)?.CalculatedQuantity == 28000, "D first reading survives process restart");
        repository.Save(Request(2, DateTime.Today));
        var second = repository.Load().Latest(id)!;
        Check(second.CalculatedQuantity == 14000 && ConsumableInventoryRules.Status(Item(), second) == ConsumableStockStatus.ToOrder, "B SQL 2 x 7000 = 14000 / Da ordinare");
        var changed = Item(); changed.QuantityPerUnit = 7500; changed.ProductName = "TEST RINOMINATO"; changed.UnitOfMeasure = "confezione"; catalog.Save(changed);
        var historical = repository.Load().Readings.First(x => x.ConsumableItemId == id && x.CountedUnits == 4);
        Check(historical.CalculatedQuantity == 28000 && historical.QuantityPerUnitSnapshot == 7000 && historical.UnitOfMeasureSnapshot == "scatola" && historical.ProductSnapshot == "TEST SQL INVENTARIO", "C immutable history after catalog changes");
        break;
    case "verify":
        var restarted = repository.Load();
        Check(restarted.Latest(id)?.CalculatedQuantity == 14000 && restarted.Latest(id)?.InventoryDate == DateTime.Today && restarted.Readings.Count(x => x.ConsumableItemId == id) == 2, "D latest inventory, stock and history survive another process");
        var backdated = Request(1, DateTime.Today.AddDays(-2)); repository.Save(backdated);
        Check(repository.Load().Latest(id)?.CalculatedQuantity == 14000, "backdated insertion does not replace latest inventory");
        var staleRequest = Request(1, DateTime.Today);
        var currentItem = Item(); currentItem.Notes = "changed concurrently"; catalog.Save(currentItem);
        Expect<DbUpdateConcurrencyException>(() => repository.Save(staleRequest), "concurrent catalog change rejected");
        using (var db = factory.CreateDbContext()) Check(!db.ConsumableInventorySessions.Any(x => x.Id == staleRequest.SessionId), "conflict leaves no partial session");
        var duplicate = Request(1, DateTime.Today);
        Expect<InvalidOperationException>(() => repository.Save(duplicate with { Readings = [duplicate.Readings[0], duplicate.Readings[0]] }), "duplicate item blocked");
        var failRequest = Request(1, DateTime.Today);
        var interceptor = new FailReadingInsert();
        var failFactory = new TestFactory(settings.BuildConnectionString(), interceptor);
        Expect<DbUpdateException>(() => new SqlConsumableInventoryRepository(failFactory).Save(failRequest), "database failure on reading insert");
        using (var db = factory.CreateDbContext())
        {
            Check(interceptor.SessionInserted && !db.ConsumableInventorySessions.Any(x => x.Id == failRequest.SessionId) && !db.ConsumableInventoryReadings.Any(x => x.SessionId == failRequest.SessionId), "atomic rollback after session INSERT");
            var badSession = new ConsumableInventorySessionEntity { Id = Guid.NewGuid(), InventoryDate = DateTime.Today, OperatorId = failRequest.OperatorId, OperatorSnapshot = "TEST", RequestHash = "TEST" };
            for (var index = 0; index < 2; index++) badSession.Readings.Add(new() { Id = Guid.NewGuid(), ConsumableItemId = id, CountedUnits = 1, QuantityPerUnitSnapshot = 1, CalculatedQuantity = 1 });
            db.Add(badSession);
            Expect<DbUpdateException>(() => db.SaveChanges(), "SQL unique SessionId + ItemId enforced");
        }
        var disabled = Item(); disabled.IsActive = false; catalog.Save(disabled);
        Expect<DbUpdateConcurrencyException>(() => repository.Save(Request(1, DateTime.Today)), "inactive article rejected");
        var viewModel = new ConsumablesViewModel(new ConsumableInventoryService(repository)); viewModel.Reload();
        Check(viewModel.InventoryRows.All(x => x.Item.Id != id) && viewModel.HistoryRows.Any(x => x.Reading.ConsumableItemId == id), "inactive excluded from inventory but history retained");
        break;
    case "cleanup":
        using (var db = factory.CreateDbContext())
        {
            var itemIds = db.ConsumableItems.Where(x => x.InternalCode == code || x.InternalCode == emptyCode).Select(x => x.Id).ToArray();
            var sessionIds = db.ConsumableInventoryReadings.Where(x => itemIds.Contains(x.ConsumableItemId)).Select(x => x.SessionId).Distinct().ToArray();
            db.ConsumableInventoryReadings.Where(x => itemIds.Contains(x.ConsumableItemId)).ExecuteDelete();
            db.ConsumableInventorySessions.Where(x => sessionIds.Contains(x.Id)).ExecuteDelete();
            db.ConsumableItems.Where(x => itemIds.Contains(x.Id)).ExecuteDelete();
            Console.WriteLine("PASS fixture cleanup");
        }
        break;
    default: throw new Exception("Unknown test step");
}

sealed class StubRepository(ConsumableInventorySnapshot snapshot) : IConsumableInventoryRepository
{
    public bool FailLoad { get; set; }
    public int Saves { get; private set; }
    public ConsumableInventorySnapshot Load() => FailLoad ? throw new InvalidOperationException("Database unavailable") : snapshot;
    public void Save(SaveConsumableInventory request) => Saves++;
}
sealed class TestFactory(string connectionString, DbCommandInterceptor interceptor) : IDbContextFactory<MagazzinoDbContext>
{
    public MagazzinoDbContext CreateDbContext() => new(new DbContextOptionsBuilder<MagazzinoDbContext>()
        .UseSqlServer(connectionString, sql => sql.MaxBatchSize(1)).AddInterceptors(interceptor).Options);
}
sealed class FailReadingInsert : DbCommandInterceptor
{
    public bool SessionInserted { get; private set; }
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        if (command.CommandText.Contains("INSERT INTO [ConsumableInventorySessions]")) SessionInserted = true;
        return result;
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        if (command.CommandText.Contains("INSERT INTO [ConsumableInventoryReadings]")) throw new InvalidOperationException("Injected test reading failure");
        return result;
    }
}
