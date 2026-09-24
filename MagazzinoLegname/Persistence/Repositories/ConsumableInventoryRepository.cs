using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence.Repositories;

public interface IConsumableInventoryRepository
{
    ConsumableInventorySnapshot Load();
    void Save(SaveConsumableInventory request);
}

// Sessions are append-only: no update/delete API for confirmed inventory.
public sealed class SqlConsumableInventoryRepository(IDbContextFactory<MagazzinoDbContext> factory) : IConsumableInventoryRepository
{
    public ConsumableInventorySnapshot Load()
    {
        using var strategyContext = factory.CreateDbContext();
        return strategyContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var db = factory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            var items = db.ConsumableItems.AsNoTracking().OrderBy(x => x.ProductName).AsEnumerable().Select(SqlConsumableRepository.Map).ToArray();
            var operators = db.Operators.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
                .Select(x => new Operator { Id = x.Id, FirstName = x.FirstName, LastName = x.LastName, IsActive = x.IsActive }).ToArray();
            var readings = db.ConsumableInventoryReadings.AsNoTracking()
                .OrderByDescending(x => x.Session.InventoryDate).ThenByDescending(x => x.Session.CreatedAtUtc)
                .ThenByDescending(x => x.SessionId).ThenByDescending(x => x.Id)
                .Select(x => new ConsumableSqlReading(x.Id, x.SessionId, x.ConsumableItemId, x.Session.InventoryDate,
                    x.Session.CreatedAtUtc, x.Session.OperatorSnapshot, x.ProductSnapshot, x.SupplierSnapshot,
                    x.DepartmentSnapshot, x.UnitOfMeasureSnapshot, x.CountedUnits, x.QuantityPerUnitSnapshot,
                    x.CalculatedQuantity, x.Note)).ToArray();
            var orders = db.ConsumableOrders.AsNoTracking().OrderByDescending(x => x.OrderedAt)
                .ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).AsEnumerable().Select(SqlConsumableOrderRepository.Map).ToArray();
            // SQL SUM of open orders only, grouped by original UDM so unlike units are never silently added.
            var totals = db.ConsumableOrders.AsNoTracking()
                .Where(x => x.Status == ConsumableOrderStatus.Ordered || x.Status == ConsumableOrderStatus.PartiallyReceived)
                .GroupBy(x => new { x.ConsumableItemId, x.UnitOfMeasureSnapshot })
                .Select(group => new ConsumableOpenOrderTotal(group.Key.ConsumableItemId, group.Key.UnitOfMeasureSnapshot,
                    group.Sum(x => x.OrderedQuantity), group.Count())).ToArray();
            transaction.Commit();
            return new ConsumableInventorySnapshot(items, operators, readings) { Orders = orders, OpenOrderTotals = totals };
        });
    }

    public void Save(SaveConsumableInventory request)
    {
        if (request.SessionId == Guid.Empty || request.OperatorId == Guid.Empty || request.Readings.Count == 0)
            throw new InvalidOperationException("Selezionare l'operatore e inserire almeno una rilevazione.");
        if (request.Readings.Select(x => x.ConsumableItemId).Distinct().Count() != request.Readings.Count)
            throw new InvalidOperationException("Un articolo non può comparire due volte nella stessa sessione.");
        var inputs = request.Readings.OrderBy(x => x.ConsumableItemId).ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Date = request.InventoryDate.Date, request.OperatorId,
            Rows = inputs.Select(x => new { x.ConsumableItemId, x.CountedUnits, Version = Convert.ToHexString(x.ItemRowVersion), Note = x.Note.Trim() })
        })));
        using var strategyContext = factory.CreateDbContext();
        strategyContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var db = factory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            var existing = db.ConsumableInventorySessions.AsNoTracking().SingleOrDefault(x => x.Id == request.SessionId);
            if (existing is not null)
            {
                if (existing.RequestHash != hash)
                    throw new DbUpdateConcurrencyException("La sessione è già stata confermata con dati diversi. Ricaricare da SQL.");
                transaction.Commit();
                return; // Safe retry after an ambiguous commit; never duplicate a session.
            }
            var operatorEntity = db.Operators.AsNoTracking().SingleOrDefault(x => x.Id == request.OperatorId && x.IsActive)
                ?? throw new InvalidOperationException("Operatore non più attivo o disponibile. Aggiornare da SQL.");
            var session = new ConsumableInventorySessionEntity
            {
                Id = request.SessionId, InventoryDate = request.InventoryDate.Date, OperatorId = operatorEntity.Id,
                OperatorSnapshot = $"{operatorEntity.FirstName} {operatorEntity.LastName}".Trim(), RequestHash = hash
            };
            var ids = inputs.Select(x => x.ConsumableItemId).ToArray();
            var items = db.ConsumableItems.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionary(x => x.Id);
            foreach (var input in inputs)
            {
                if (!items.TryGetValue(input.ConsumableItemId, out var item) || !item.IsActive ||
                    !input.ItemRowVersion.SequenceEqual(item.RowVersion))
                    throw new DbUpdateConcurrencyException("Anagrafica modificata, disattivata o eliminata durante il conteggio. Aggiornare da SQL e verificare le UDM rilevate.");
                if (input.Note.Trim().Length > 2000) throw new InvalidOperationException("Le note accettano al massimo 2000 caratteri.");
                var quantityPerUnit = item.QuantityPerUnit ?? throw new InvalidOperationException($"{item.ProductName}: impostare Qtà per UDM nell'anagrafica.");
                session.Readings.Add(new ConsumableInventoryReadingEntity
                {
                    Id = Guid.NewGuid(), ConsumableItemId = item.Id, SessionId = session.Id,
                    CountedUnits = input.CountedUnits, QuantityPerUnitSnapshot = quantityPerUnit,
                    CalculatedQuantity = ConsumableInventoryRules.Calculate(input.CountedUnits, quantityPerUnit),
                    ProductSnapshot = item.ProductName, SupplierSnapshot = item.SupplierName,
                    DepartmentSnapshot = item.Department, UnitOfMeasureSnapshot = item.UnitOfMeasure, Note = input.Note.Trim()
                });
            }
            db.ConsumableInventorySessions.Add(session); // All new graph entities are Added.
            db.SaveChanges();
            transaction.Commit();
        });
    }
}
