using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence.Repositories;

public interface IConsumableRepository
{
    IReadOnlyList<ConsumableItem> GetAll();
    ConsumableItem? Get(Guid id);
    void Save(ConsumableItem item);
}

public sealed class SqlConsumableRepository(IDbContextFactory<MagazzinoDbContext> contextFactory) : IConsumableRepository
{
    public IReadOnlyList<ConsumableItem> GetAll()
    {
        using var db = contextFactory.CreateDbContext();
        return db.ConsumableItems.AsNoTracking().OrderBy(x => x.ProductName).AsEnumerable().Select(Map).ToList();
    }

    public ConsumableItem? Get(Guid id)
    {
        using var db = contextFactory.CreateDbContext();
        var entity = db.ConsumableItems.AsNoTracking().SingleOrDefault(x => x.Id == id);
        return entity is null ? null : Map(entity);
    }

    public void Save(ConsumableItem item)
    {
        Validate(item);
        using var db = contextFactory.CreateDbContext();
        var entity = new ConsumableItemEntity
        {
            Id = item.Id,
            InternalCode = item.InternalCode.Trim(),
            ProductName = item.ProductName.Trim(),
            SupplierName = item.SupplierName.Trim(),
            Department = item.Department.Trim(),
            UnitOfMeasure = item.UnitOfMeasure.Trim(),
            QuantityPerUnit = item.QuantityPerUnit,
            MinimumStock = item.MinimumStock,
            ConsumptionAverageText = item.ConsumptionAverageText.Trim(),
            ConsumptionAverageQuantity = item.ConsumptionAverageQuantity,
            ConsumptionPeriod = item.ConsumptionPeriod,
            LeadTimeDays = item.LeadTimeDays,
            LeadTimeText = item.LeadTimeText.Trim(),
            Packaging = item.Packaging.Trim(),
            Notes = item.Notes.Trim(),
            IsActive = item.IsActive,
        };
        if (item.RowVersion.Length == 0)
            db.ConsumableItems.Add(entity);
        else
        {
            // The token supplied by the editing client is used in the UPDATE predicate.
            // A deleted row is a conflict too: never turn an UPDATE into an INSERT.
            db.ConsumableItems.Attach(entity);
            db.Entry(entity).State = EntityState.Modified;
            db.Entry(entity).Property(x => x.RowVersion).OriginalValue = item.RowVersion.ToArray();
            db.Entry(entity).Property(x => x.RowVersion).IsModified = false;
        }
        db.SaveChanges();
        item.RowVersion = entity.RowVersion.ToArray();
    }

    private static void Validate(ConsumableItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ProductName)) throw new InvalidOperationException("Il nome prodotto è obbligatorio.");
        if (string.IsNullOrWhiteSpace(item.InternalCode)) throw new InvalidOperationException("Il codice articolo è obbligatorio.");
        if (item.QuantityPerUnit is <= 0) throw new InvalidOperationException("Qtà per UDM deve essere maggiore di zero quando valorizzata.");
        if (item.MinimumStock is < 0 || item.ConsumptionAverageQuantity is < 0 || item.LeadTimeDays is < 0)
            throw new InvalidOperationException("Scorta minima, consumo medio e lead time non possono essere negativi.");
        foreach (var value in new[] { item.QuantityPerUnit, item.MinimumStock, item.ConsumptionAverageQuantity })
            if (value.HasValue && (value.Value >= 10000000000000m || decimal.Round(value.Value, 6) != value.Value))
                throw new InvalidOperationException("Le quantità accettano al massimo 13 cifre intere e 6 decimali.");
        if (!Enum.IsDefined(item.ConsumptionPeriod)) throw new InvalidOperationException("Periodicità non valida.");
        if (item.InternalCode.Trim().Length > 40) throw new InvalidOperationException("InternalCode: massimo 40 caratteri.");
        if (item.ProductName.Trim().Length > 300) throw new InvalidOperationException("ProductName: massimo 300 caratteri.");
        if (item.SupplierName.Trim().Length > 300) throw new InvalidOperationException("SupplierName: massimo 300 caratteri.");
        if (item.Department.Trim().Length > 200) throw new InvalidOperationException("Department: massimo 200 caratteri.");
        if (item.UnitOfMeasure.Trim().Length > 40) throw new InvalidOperationException("UnitOfMeasure: massimo 40 caratteri.");
        if (item.ConsumptionAverageText.Trim().Length > 500) throw new InvalidOperationException("ConsumptionAverageText: massimo 500 caratteri.");
        if (item.LeadTimeText.Trim().Length > 500) throw new InvalidOperationException("LeadTimeText: massimo 500 caratteri.");
        if (item.Packaging.Trim().Length > 1000) throw new InvalidOperationException("Packaging: massimo 1000 caratteri.");
    }

    private static ConsumableItem Map(ConsumableItemEntity entity) => new()
    {
        Id = entity.Id, RowVersion = entity.RowVersion.ToArray(),
        InternalCode = entity.InternalCode,
        ProductName = entity.ProductName,
        SupplierName = entity.SupplierName,
        Department = entity.Department,
        UnitOfMeasure = entity.UnitOfMeasure,
        QuantityPerUnit = entity.QuantityPerUnit,
        MinimumStock = entity.MinimumStock,
        ConsumptionAverageText = entity.ConsumptionAverageText,
        ConsumptionAverageQuantity = entity.ConsumptionAverageQuantity,
        ConsumptionPeriod = entity.ConsumptionPeriod,
        LeadTimeDays = entity.LeadTimeDays,
        LeadTimeText = entity.LeadTimeText,
        Packaging = entity.Packaging,
        Notes = entity.Notes,
        IsActive = entity.IsActive,
    };
}
