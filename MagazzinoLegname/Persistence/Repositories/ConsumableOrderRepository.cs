using System.Data;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence.Repositories;

public interface IConsumableOrderRepository
{
    ConsumableSqlOrder? Get(Guid id);
    void Save(ConsumableSqlOrder order);
}

public sealed class SqlConsumableOrderRepository(IDbContextFactory<MagazzinoDbContext> factory) : IConsumableOrderRepository
{
    public ConsumableSqlOrder? Get(Guid id)
    {
        using var db = factory.CreateDbContext();
        var entity = db.ConsumableOrders.AsNoTracking().SingleOrDefault(x => x.Id == id);
        return entity is null ? null : Map(entity);
    }

    public void Save(ConsumableSqlOrder order)
    {
        ConsumableOrderRules.Validate(order);
        using var strategyContext = factory.CreateDbContext();
        var savedVersion = strategyContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var db = factory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            ConsumableOrderEntity entity;
            if (order.RowVersion.Length == 0)
            {
                if (db.ConsumableOrders.Any(x => x.Id == order.Id))
                    throw new DbUpdateConcurrencyException("Questo ordine è già stato registrato. Ricaricare gli ordini da SQL.");
                var item = db.ConsumableItems.AsNoTracking().SingleOrDefault(x => x.Id == order.MaterialId)
                    ?? throw new InvalidOperationException("L'articolo non esiste più. Ricaricare da SQL.");
                if (!item.IsActive) throw new InvalidOperationException("Non è possibile creare un ordine per un articolo inattivo.");
                if (!order.ItemRowVersion.SequenceEqual(item.RowVersion))
                    throw new DbUpdateConcurrencyException("L'anagrafica è cambiata durante la compilazione dell'ordine. Ricaricare e verificare quantità e UDM.");
                entity = new ConsumableOrderEntity
                {
                    Id = order.Id, ConsumableItemId = item.Id, SupplierNameSnapshot = item.SupplierName,
                    ProductNameSnapshot = item.ProductName, UnitOfMeasureSnapshot = item.UnitOfMeasure
                };
                db.ConsumableOrders.Add(entity);
            }
            else
            {
                entity = db.ConsumableOrders.SingleOrDefault(x => x.Id == order.Id)
                    ?? throw new DbUpdateConcurrencyException("L'ordine non è più disponibile.");
                if (!order.RowVersion.SequenceEqual(entity.RowVersion))
                    throw new DbUpdateConcurrencyException("L'ordine è stato modificato o chiuso da un'altra postazione.");
                if (entity.ConsumableItemId != order.MaterialId) throw new InvalidOperationException("Non è possibile cambiare l'articolo di un ordine.");
                if (entity.Status is not (ConsumableOrderStatus.Ordered or ConsumableOrderStatus.PartiallyReceived))
                    throw new InvalidOperationException("Un ordine ricevuto o annullato è consultabile, ma non modificabile.");
                db.Entry(entity).Property(x => x.RowVersion).OriginalValue = order.RowVersion.ToArray();
            }
            entity.OrderedAt = order.OrderDate!.Value.Date;
            entity.OrderedQuantity = order.Quantity!.Value;
            entity.ExpectedDeliveryDate = order.ExpectedDeliveryDate?.Date;
            entity.Status = order.Status;
            entity.Notes = order.Note.Trim();
            if (!order.IsOpen)
                entity.ClosedAtUtc = db.Database.SqlQueryRaw<DateTime>("SELECT SYSUTCDATETIME() AS [Value]").Single();
            db.SaveChanges();
            transaction.Commit();
            return entity.RowVersion.ToArray();
        });
        order.RowVersion = savedVersion;
    }

    internal static ConsumableSqlOrder Map(ConsumableOrderEntity entity) => new()
    {
        Id = entity.Id, MaterialId = entity.ConsumableItemId, OrderDate = entity.OrderedAt,
        Quantity = entity.OrderedQuantity, ExpectedDeliveryDate = entity.ExpectedDeliveryDate,
        SupplierNameSnapshot = entity.SupplierNameSnapshot, ProductNameSnapshot = entity.ProductNameSnapshot,
        UnitOfMeasureSnapshot = entity.UnitOfMeasureSnapshot, Status = entity.Status, Note = entity.Notes,
        CreatedAtUtc = entity.CreatedAtUtc, ClosedAtUtc = entity.ClosedAtUtc, RowVersion = entity.RowVersion.ToArray()
    };
}
