using MagazzinoLegname.Models;
namespace MagazzinoLegname.Persistence.Entities;
public sealed class PlannedArrivalEntity
{
 public Guid Id { get; set; }
 public DateTime Date { get; set; }
 public Guid SupplierId { get; set; }
 public string SupplierNameSnapshot { get; set; } = "";
 public decimal ConventionalThickness { get; set; }
 public string Quality { get; set; } = "";
 public int LoadQuantity { get; set; }
 public decimal ExpectedCubicMeters { get; set; }
 public string Notes { get; set; } = "";
 public DateTime CreatedAtUtc { get; set; }
 public DateTime UpdatedAtUtc { get; set; }
 public byte[] RowVersion { get; set; } = [];
 public PlannedArrivalStatus Status { get; set; }
 public DateTime? ConfirmedAt { get; set; }
 public string? ConfirmedBy { get; set; }
}
