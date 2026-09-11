using MagazzinoLegname.Models;

namespace MagazzinoLegname.Persistence.Entities;

public sealed class ConsumableItemEntity
{
    public Guid Id { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal? QuantityPerUnit { get; set; }
    public decimal? MinimumStock { get; set; }
    public string ConsumptionAverageText { get; set; } = string.Empty;
    public decimal? ConsumptionAverageQuantity { get; set; }
    public ConsumptionPeriod ConsumptionPeriod { get; set; }
    public int? LeadTimeDays { get; set; }
    public string LeadTimeText { get; set; } = string.Empty;
    public string Packaging { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
