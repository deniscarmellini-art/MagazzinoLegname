using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class ConsumableSqlOrder : ObservableObject
{
    private decimal? _quantity;
    private DateTime? _orderDate = DateTime.Today, _expectedDeliveryDate;
    private string _note = string.Empty;
    private ConsumableOrderStatus _status = ConsumableOrderStatus.Ordered;
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MaterialId { get; init; }
    public byte[] RowVersion { get; set; } = [];
    public byte[] ItemRowVersion { get; init; } = [];
    public string SupplierNameSnapshot { get; init; } = string.Empty;
    public string ProductNameSnapshot { get; init; } = string.Empty;
    public string UnitOfMeasureSnapshot { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    // Same semantics as the legacy editor: direct quantity, never package count x QuantityPerUnit.
    public decimal? Quantity { get => _quantity; set => SetProperty(ref _quantity, value); }
    public DateTime? OrderDate { get => _orderDate; set => SetProperty(ref _orderDate, value); }
    public DateTime? ExpectedDeliveryDate { get => _expectedDeliveryDate; set => SetProperty(ref _expectedDeliveryDate, value); }
    public string Note { get => _note; set => SetProperty(ref _note, value); }
    public ConsumableOrderStatus Status { get => _status; set => SetProperty(ref _status, value); }
    public bool IsOpen => Status is ConsumableOrderStatus.Ordered or ConsumableOrderStatus.PartiallyReceived;
    public string StatusDisplay => StatusLabel(Status);
    public string DisplayName => $"{OrderDate:dd/MM/yyyy} · {Quantity:0.######} {UnitOfMeasureSnapshot} · {StatusDisplay} · {Id.ToString()[..8]}";
    public string ClosedDisplay => ClosedAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";
    public ConsumableSqlOrder Copy() => new()
    {
        Id = Id, MaterialId = MaterialId, RowVersion = RowVersion.ToArray(), ItemRowVersion = ItemRowVersion.ToArray(),
        SupplierNameSnapshot = SupplierNameSnapshot, ProductNameSnapshot = ProductNameSnapshot, UnitOfMeasureSnapshot = UnitOfMeasureSnapshot,
        CreatedAtUtc = CreatedAtUtc, ClosedAtUtc = ClosedAtUtc, Quantity = Quantity, OrderDate = OrderDate,
        ExpectedDeliveryDate = ExpectedDeliveryDate, Note = Note, Status = Status
    };
    public static string StatusLabel(ConsumableOrderStatus status) => status switch
    {
        ConsumableOrderStatus.Ordered => "In ordine", ConsumableOrderStatus.PartiallyReceived => "Ricevuto parzialmente",
        ConsumableOrderStatus.Received => "Ricevuto / chiuso", ConsumableOrderStatus.Cancelled => "Annullato", _ => "Nessuno"
    };
}

public sealed record ConsumableOpenOrderTotal(Guid MaterialId, string UnitOfMeasure, decimal Quantity, int Count);

public static class ConsumableOrderRules
{
    public static ConsumableStockStatus Status(ConsumableItem item, ConsumableSqlReading? reading, bool hasOpenOrders)
    {
        var basis = ConsumableInventoryRules.Status(item, reading);
        if (!hasOpenOrders || basis == ConsumableStockStatus.ToVerify) return basis;
        return basis == ConsumableStockStatus.ToOrder ? ConsumableStockStatus.BelowMinimumOrdered : ConsumableStockStatus.Ordered;
    }
    public static void Validate(ConsumableSqlOrder order)
    {
        if (order.Id == Guid.Empty || order.MaterialId == Guid.Empty) throw new InvalidOperationException("Ordine o articolo non valido.");
        if (!order.OrderDate.HasValue) throw new InvalidOperationException("La data ordine è obbligatoria.");
        if (order.Quantity is null or <= 0 || order.Quantity >= 10000000000000m || decimal.Round(order.Quantity.Value, 6) != order.Quantity)
            throw new InvalidOperationException("Inserire una quantità positiva con massimo 13 cifre intere e 6 decimali.");
        if (order.Status is not (ConsumableOrderStatus.Ordered or ConsumableOrderStatus.PartiallyReceived or ConsumableOrderStatus.Received or ConsumableOrderStatus.Cancelled))
            throw new InvalidOperationException("Selezionare uno stato ordine valido.");
        if (order.RowVersion.Length == 0 && !order.IsOpen) throw new InvalidOperationException("Un nuovo ordine deve essere aperto.");
        if (order.ExpectedDeliveryDate?.Date < order.OrderDate.Value.Date) throw new InvalidOperationException("La consegna prevista non può precedere la data ordine.");
        if (order.Note.Length > 2000) throw new InvalidOperationException("Le note accettano al massimo 2000 caratteri.");
    }
}
