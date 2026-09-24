using MagazzinoLegname.Models;

namespace MagazzinoLegname.Persistence.Entities;

public sealed class ConsumableOrderEntity
{
    public Guid Id { get; set; }
    public Guid ConsumableItemId { get; set; }
    public DateTime OrderedAt { get; set; }
    public decimal OrderedQuantity { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string SupplierNameSnapshot { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string UnitOfMeasureSnapshot { get; set; } = string.Empty;
    public ConsumableOrderStatus Status { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
