namespace MagazzinoLegname.Persistence.Entities;

public sealed class ConsumableInventorySessionEntity
{
    public Guid Id { get; set; }
    public DateTime InventoryDate { get; set; }
    public Guid OperatorId { get; set; }
    public string OperatorSnapshot { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public List<ConsumableInventoryReadingEntity> Readings { get; set; } = [];
}

public sealed class ConsumableInventoryReadingEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public ConsumableInventorySessionEntity Session { get; set; } = null!;
    public Guid ConsumableItemId { get; set; }
    public decimal CountedUnits { get; set; }
    public decimal QuantityPerUnitSnapshot { get; set; }
    public decimal CalculatedQuantity { get; set; }
    public string ProductSnapshot { get; set; } = string.Empty;
    public string SupplierSnapshot { get; set; } = string.Empty;
    public string DepartmentSnapshot { get; set; } = string.Empty;
    public string UnitOfMeasureSnapshot { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}
