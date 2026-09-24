using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Repositories;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;
using Microsoft.EntityFrameworkCore;

static void Check(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL " + label);
    Console.WriteLine("PASS " + label);
}
static T Expect<T>(Action action, string label) where T : Exception
{
    try { action(); }
    catch (T ex) { Console.WriteLine("PASS " + label); return ex; }
    throw new Exception("FAIL expected " + typeof(T).Name + ": " + label);
}

if (args[0] == "offline")
{
    var item = new ConsumableItem { MinimumStock = 20000, QuantityPerUnit = 7000 };
    var reading = new ConsumableSqlReading(Guid.NewGuid(), Guid.NewGuid(), item.Id, DateTime.Today, DateTime.UtcNow,
        "Operator", "Product", "", "", "scatola", 2, 7000, 14000, "");
    Check(ConsumableOrderRules.Status(item, reading, false) == ConsumableStockStatus.ToOrder, "A base below minimum");
    Check(ConsumableOrderRules.Status(item, reading, true) == ConsumableStockStatus.BelowMinimumOrdered, "B below minimum with open order");
    Check(ConsumableOrderRules.Status(item, reading with { CountedUnits = 4, CalculatedQuantity = 28000 }, true) == ConsumableStockStatus.Ordered, "C above minimum with open order");
    Check(ConsumableOrderRules.Status(item, reading with { CalculatedQuantity = 20000 }, false) == ConsumableStockStatus.Ok, "equality and no open orders");
    Check(ConsumableOrderRules.Status(item, null, true) == ConsumableStockStatus.ToVerify, "missing reading has priority over open orders");
    var order = new ConsumableSqlOrder { MaterialId = item.Id, Quantity = 10, UnitOfMeasureSnapshot = "scatola" };
    ConsumableOrderRules.Validate(order);
    order.Quantity = -1; Expect<InvalidOperationException>(() => ConsumableOrderRules.Validate(order), "negative quantity rejected");
    order.Quantity = 0; Expect<InvalidOperationException>(() => ConsumableOrderRules.Validate(order), "zero quantity rejected");
    order.Quantity = .0000001m; Expect<InvalidOperationException>(() => ConsumableOrderRules.Validate(order), "excess decimal precision rejected");
    order.Quantity = 1; order.Status = ConsumableOrderStatus.None;
    Expect<InvalidOperationException>(() => ConsumableOrderRules.Validate(order), "None cannot be saved as SQL order");
    order.Status = ConsumableOrderStatus.Ordered; order.RowVersion = [1];
    var row = new ConsumableSituationRow(item, reading, [order], [new(item.Id, "scatola", 10, 1)], ConsumableStockStatus.BelowMinimumOrdered);
    row.Order.Quantity = 99;
    Check(order.Quantity == 1 && row.Totals[0].Quantity == 10, "draft does not change authoritative totals");
    row.CreateNewOrder(); Check(row.Order.RowVersion.Length == 0 && row.Order.Id != order.Id, "new order is a distinct draft");
    return;
}

var (settings, _) = DatabaseSettingsLoader.Load();
if (settings.Database != "MagazzinoLegname_Dev") throw new Exception("SQL tests require MagazzinoLegname_Dev.");
var suffix = Guid.Parse(args[1]).ToString("N");
var codes = new[] { "OL-" + suffix, "OH-" + suffix, "ON-" + suffix, "OI-" + suffix };
var factory = new MagazzinoDbContextFactory();
var catalog = new SqlConsumableRepository(factory);
var inventory = new SqlConsumableInventoryRepository(factory);
var repository = new SqlConsumableOrderRepository(factory);
ConsumableItem Item(int index) => catalog.GetAll().Single(x => x.InternalCode == codes[index]);
ConsumableInventorySnapshot Snapshot() => inventory.Load();
ConsumableSqlOrder Draft(int index, decimal quantity, string note) { var item = Item(index); return new() { MaterialId = item.Id, ItemRowVersion = item.RowVersion, Quantity = quantity, Note = note, OrderDate = DateTime.Today, ExpectedDeliveryDate = DateTime.Today.AddDays(2) }; }
ConsumableStockStatus Status(int index) { var snapshot = Snapshot(); var item = snapshot.Items.Single(x => x.InternalCode == codes[index]); return ConsumableOrderRules.Status(item, snapshot.Latest(item.Id), snapshot.HasOpenOrders(item.Id)); }
decimal Total(int index) => Snapshot().OpenOrderTotals.Where(x => x.MaterialId == Item(index).Id).Sum(x => x.Quantity);
ConsumableSqlOrder Order(string note) => Snapshot().Orders.Single(x => x.Note == suffix + note);
void CheckInventoryUnchanged()
{
    var snapshot = Snapshot();
    Check(snapshot.Latest(Item(0).Id)?.CalculatedQuantity == 14000 && snapshot.Latest(Item(1).Id)?.CalculatedQuantity == 28000 && snapshot.Readings.Count(x => x.ConsumableItemId == Item(0).Id || x.ConsumableItemId == Item(1).Id) == 2, "orders never modify stock or add inventory readings");
}

switch (args[0])
{
    case "create":
        using (var db = factory.CreateDbContext())
        {
            Check(!db.Database.HasPendingModelChanges(), "EF model matches migration");
            var pending = db.Database.GetPendingMigrations().ToArray();
            if (pending.Any(x => !x.EndsWith("_AddConsumableOrders", StringComparison.Ordinal))) throw new Exception("Unexpected pending migration.");
            db.Database.Migrate();
        }
        for (var index = 0; index < codes.Length; index++) catalog.Save(new ConsumableItem
        {
            InternalCode = codes[index], ProductName = "TEST ORDINI " + index, UnitOfMeasure = "scatola",
            QuantityPerUnit = 7000, MinimumStock = 20000, IsActive = index != 3
        });
        var op = Snapshot().Operators.FirstOrDefault() ?? throw new Exception("An active SQL operator is required.");
        inventory.Save(new(Guid.NewGuid(), DateTime.Today, op.Id,
            [new(Item(0).Id, 2, Item(0).RowVersion, "TEST ORDINI"), new(Item(1).Id, 4, Item(1).RowVersion, "TEST ORDINI")]));
        Check(Status(0) == ConsumableStockStatus.ToOrder && Total(0) == 0, "A SQL Da ordinare");
        var low = Draft(0, 1000, suffix + "B"); repository.Save(low);
        Check(low.RowVersion.Length == 8 && Status(0) == ConsumableStockStatus.BelowMinimumOrdered && Total(0) == 1000, "B open order and direct quantity (no x7000)");
        var high = Draft(1, 2500, suffix + "C"); repository.Save(high);
        Check(Status(1) == ConsumableStockStatus.Ordered && Total(1) == 2500, "C SQL In ordine above minimum");
        repository.Save(Draft(2, 1, suffix + "N"));
        Check(Status(2) == ConsumableStockStatus.ToVerify && Total(2) == 1, "missing inventory remains Da verificare with an open order");
        Expect<InvalidOperationException>(() => repository.Save(Draft(3, 1, suffix + "inactive")), "new order on inactive item rejected");
        CheckInventoryUnchanged();
        break;
    case "update":
        Check(Total(0) == 1000 && Status(0) == ConsumableStockStatus.BelowMinimumOrdered && Total(1) == 2500, "F open orders and states survive process restart");
        var vm = new ConsumablesViewModel(new ConsumableInventoryService(inventory), new ConsumableOrderService(repository));
        vm.Reload(); vm.SelectedSituation = vm.SituationRows.Single(x => x.Item.Id == Item(0).Id);
        vm.SelectedSituation.Order.Status = ConsumableOrderStatus.Received;
        vm.SaveOrders();
        Check(Total(0) == 0 && Status(0) == ConsumableStockStatus.ToOrder && Order("B").ClosedAtUtc.HasValue, "D closure removes open quantity and restores Da ordinare");
        Check(vm.SelectedSituation?.Status == ConsumableStockStatus.ToOrder && vm.OrderHistory.Any(x => x.Id == Order("B").Id && x.Status == ConsumableOrderStatus.Received), "SQL reload refreshes situation and history");
        var first = Draft(0, 100, suffix + "E1"); var second = Draft(0, 200, suffix + "E2");
        Task.WaitAll(Task.Run(() => repository.Save(first)), Task.Run(() => repository.Save(second)));
        Check(Total(0) == 300, "E concurrent distinct orders sum to 300 in SQL");
        var changed = repository.Get(first.Id)!; changed.Quantity = 150; changed.Status = ConsumableOrderStatus.PartiallyReceived; repository.Save(changed);
        Check(Total(0) == 350, "open quantity edit and partial status contribute entered quantity");
        vm.Reload(); vm.SelectedSituation = vm.SituationRows.Single(x => x.Item.Id == Item(0).Id);
        vm.SelectedSituation.SelectedOrder = vm.SelectedSituation.Orders.Single(x => x.Id == first.Id);
        var clientA = repository.Get(first.Id)!; clientA.Status = ConsumableOrderStatus.Received; repository.Save(clientA);
        vm.SelectedSituation.Order.Quantity = 999;
        var conflict = Expect<InvalidOperationException>(() => vm.SaveOrders(), "G stale client edit after closure is handled");
        Check(conflict.Message.Contains("ricaricati da SQL") && repository.Get(first.Id)!.Quantity == 150 && repository.Get(first.Id)!.Status == ConsumableOrderStatus.Received && Total(0) == 200, "G clear message, SQL reload and no silent overwrite");
        var cancel = repository.Get(second.Id)!; cancel.Status = ConsumableOrderStatus.Cancelled; repository.Save(cancel);
        Check(Total(0) == 0 && repository.Get(second.Id)!.ClosedAtUtc.HasValue && Status(0) == ConsumableStockStatus.ToOrder, "cancelled order excluded but retained in SQL history");
        var closed = repository.Get(first.Id)!; closed.Status = ConsumableOrderStatus.Ordered;
        Expect<InvalidOperationException>(() => repository.Save(closed), "closed order cannot be reopened or overwritten");
        CheckInventoryUnchanged();
        break;
    case "verify":
        Check(Order("B").Status == ConsumableOrderStatus.Received && Order("E1").ClosedAtUtc.HasValue && Order("E2").Status == ConsumableOrderStatus.Cancelled && Total(0) == 0 && Total(1) == 2500, "F closure, cancellation and remaining open totals survive restart");
        var staleDraft = Draft(1, 123, suffix + "stale");
        var renamed = Item(1); renamed.ProductName = "TEST RENAMED"; renamed.UnitOfMeasure = "kg"; renamed.QuantityPerUnit = 7500; catalog.Save(renamed);
        Expect<DbUpdateConcurrencyException>(() => repository.Save(staleDraft), "new order refuses changed article snapshot");
        Check(Order("C").ProductNameSnapshot == "TEST ORDINI 1" && Order("C").UnitOfMeasureSnapshot == "scatola" && Order("C").Quantity == 2500, "order history retains name, UDM and direct quantity");
        var newUnit = Draft(1, 7, suffix + "unit"); repository.Save(newUnit);
        var unitTotals = Snapshot().OpenOrderTotals.Where(x => x.MaterialId == Item(1).Id).ToArray();
        Check(unitTotals.Length == 2 && unitTotals.Any(x => x.UnitOfMeasure == "kg" && x.Quantity == 7) && unitTotals.Any(x => x.UnitOfMeasure == "scatola" && x.Quantity == 2500), "unlike snapshot units are displayed separately");
        newUnit.Status = ConsumableOrderStatus.Cancelled; repository.Save(newUnit);
        var vm2 = new ConsumablesViewModel(new ConsumableInventoryService(inventory)); vm2.Reload();
        var snapshot2 = Snapshot();
        Check(vm2.ActiveItems == snapshot2.Items.Count(x => x.IsActive) && vm2.Ordered == snapshot2.Items.Count(x => x.IsActive && snapshot2.HasOpenOrders(x.Id)), "KPI In ordine includes all active articles with open orders");
        Check(vm2.BelowMinimum == vm2.SituationRows.Count(x => x.Status is ConsumableStockStatus.ToOrder or ConsumableStockStatus.BelowMinimumOrdered) && vm2.ToOrder == vm2.SituationRows.Count(x => x.Status == ConsumableStockStatus.ToOrder), "KPI below minimum and ToOrder have distinct meanings");
        var disabledItem = Item(1); disabledItem.IsActive = false; catalog.Save(disabledItem); vm2.Reload();
        Check(vm2.SituationRows.All(x => x.Item.Id != disabledItem.Id) && vm2.OrderHistory.Any(x => x.MaterialId == disabledItem.Id), "inactive article excluded from KPI/situation while order history remains available");
        var highClosed = Order("C"); highClosed.Status = ConsumableOrderStatus.Received; repository.Save(highClosed);
        Check(Total(1) == 0, "existing order can still be closed after article deactivation");
        CheckInventoryUnchanged();
        break;
    case "cleanup":
        using (var db = factory.CreateDbContext())
        {
            var ids = db.ConsumableItems.Where(x => codes.Contains(x.InternalCode)).Select(x => x.Id).ToArray();
            var sessions = db.ConsumableInventoryReadings.Where(x => ids.Contains(x.ConsumableItemId)).Select(x => x.SessionId).Distinct().ToArray();
            db.ConsumableOrders.Where(x => ids.Contains(x.ConsumableItemId)).ExecuteDelete();
            db.ConsumableInventoryReadings.Where(x => ids.Contains(x.ConsumableItemId)).ExecuteDelete();
            db.ConsumableInventorySessions.Where(x => sessions.Contains(x.Id)).ExecuteDelete();
            db.ConsumableItems.Where(x => ids.Contains(x.Id)).ExecuteDelete();
            Console.WriteLine("PASS fixture cleanup");
        }
        break;
    default: throw new Exception("Unknown test mode");
}
