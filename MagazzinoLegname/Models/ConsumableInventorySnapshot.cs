namespace MagazzinoLegname.Models;

public sealed record ConsumableInventoryInput(Guid ConsumableItemId, decimal CountedUnits, byte[] ItemRowVersion, string Note);
public sealed record SaveConsumableInventory(Guid SessionId, DateTime InventoryDate, Guid OperatorId, IReadOnlyList<ConsumableInventoryInput> Readings);
public sealed record ConsumableSqlReading(Guid Id, Guid SessionId, Guid ConsumableItemId, DateTime InventoryDate,
    DateTime CreatedAtUtc, string OperatorSnapshot, string ProductSnapshot, string SupplierSnapshot,
    string DepartmentSnapshot, string UnitOfMeasureSnapshot, decimal CountedUnits,
    decimal QuantityPerUnitSnapshot, decimal CalculatedQuantity, string Note);
public sealed record ConsumableInventorySnapshot(IReadOnlyList<ConsumableItem> Items,
    IReadOnlyList<Operator> Operators, IReadOnlyList<ConsumableSqlReading> Readings)
{
    public IReadOnlyList<ConsumableSqlOrder> Orders { get; init; } = [];
    public IReadOnlyList<ConsumableOpenOrderTotal> OpenOrderTotals { get; init; } = [];
    public bool HasOpenOrders(Guid itemId) => OpenOrderTotals.Any(x => x.MaterialId == itemId && x.Count > 0);
    // Readings arrive in deterministic SQL order: inventory date, creation UTC, session ID descending.
    public ConsumableSqlReading? Latest(Guid itemId) => Readings.FirstOrDefault(x => x.ConsumableItemId == itemId);
}

public static class ConsumableInventoryRules
{
    public static decimal Calculate(decimal countedUnits, decimal quantityPerUnit)
    {
        if (countedUnits < 0 || countedUnits >= 1000000000000m || decimal.Round(countedUnits, 6) != countedUnits)
            throw new InvalidOperationException("UDM rilevate: inserire un numero non negativo con massimo 12 cifre intere e 6 decimali.");
        if (quantityPerUnit <= 0 || quantityPerUnit >= 10000000000000m || decimal.Round(quantityPerUnit, 6) != quantityPerUnit)
            throw new InvalidOperationException("Qtà per UDM mancante o non valida nell'anagrafica.");
        var result = checked(countedUnits * quantityPerUnit);
        if (result >= 10000000000000000m)
            throw new InvalidOperationException("Giacenza calcolata oltre il limite consentito (16 cifre intere).");
        return result;
    }

    public static ConsumableStockStatus Status(ConsumableItem item, ConsumableSqlReading? reading)
    {
        // A valid historical snapshot remains authoritative even when the current package size changes.
        if (reading is null || item.MinimumStock is null or < 0 || reading.QuantityPerUnitSnapshot <= 0 || reading.CountedUnits < 0)
            return ConsumableStockStatus.ToVerify;
        return reading.CalculatedQuantity >= item.MinimumStock.Value ? ConsumableStockStatus.Ok : ConsumableStockStatus.ToOrder;
    }
}
