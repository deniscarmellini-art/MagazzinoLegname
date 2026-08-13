namespace MagazzinoLegname.Models;

public sealed class SupplierPrice
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid SupplierId { get; init; }
    public required decimal ConventionalThickness { get; init; }
    public required decimal PricePerCubicMeter { get; init; }
    public required DateTime ValidFrom { get; init; }
    public DateTime? ValidTo { get; set; }

    public bool IsValidOn(DateTime date) =>
        ValidFrom.Date <= date.Date && (!ValidTo.HasValue || ValidTo.Value.Date >= date.Date);
}
