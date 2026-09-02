using System.Collections.ObjectModel;
using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class ConsumablesStore
{
    private readonly object _sync = new();
    public static ConsumablesStore Shared { get; } = new();
    private ConsumablesStore() { }

    public ObservableCollection<ConsumableItem> Items { get; } = [];
    public ObservableCollection<ConsumableInventoryReading> Readings { get; } = [];
    public ObservableCollection<ConsumableOrderInfo> Orders { get; } = [];
    public HashSet<string> ImportedFingerprints { get; } = new(StringComparer.OrdinalIgnoreCase);
    public event EventHandler? Changed;

    public ConsumableItem AddItem()
    {
        var item = new ConsumableItem { InternalCode = NextCode(), ProductName = "Nuovo materiale", IsActive = true };
        Items.Add(item); NotifyChanged(); return item;
    }

    public ConsumableOrderInfo OrderFor(Guid materialId)
    {
        var order = Orders.FirstOrDefault(item => item.MaterialId == materialId);
        if (order is not null) return order;
        order = new ConsumableOrderInfo { MaterialId = materialId };
        Orders.Add(order); return order;
    }

    public ConsumableInventoryReading? LatestReading(Guid materialId) => Readings
        .Where(item => item.MaterialId == materialId)
        .OrderByDescending(item => item.ReadingDate).ThenByDescending(item => item.Id).FirstOrDefault();

    public ConsumableInventoryReading? PreviousReading(Guid materialId, ConsumableInventoryReading reading) => Readings
        .Where(item => item.MaterialId == materialId && (item.ReadingDate < reading.ReadingDate || item.ReadingDate == reading.ReadingDate && item.Id != reading.Id))
        .OrderByDescending(item => item.ReadingDate).ThenByDescending(item => item.Id).FirstOrDefault();

    public void AddReadings(IEnumerable<ConsumableInventoryReading> readings)
    {
        lock (_sync) foreach (var reading in readings) Readings.Add(reading);
        NotifyChanged();
    }

    public void Import(IEnumerable<ConsumableItem> items, IEnumerable<ConsumableInventoryReading> readings,
        IEnumerable<ConsumableOrderInfo> orders)
    {
        lock (_sync)
        {
            foreach (var item in items) Items.Add(item);
            foreach (var reading in readings) Readings.Add(reading);
            foreach (var order in orders) Orders.Add(order);
        }
        NotifyChanged();
    }

    public ConsumableStockStatus StatusFor(ConsumableItem item)
    {
        var latest = LatestReading(item.Id);
        if (item.NeedsVerification || latest is null || !item.MinimumStock.HasValue || string.IsNullOrWhiteSpace(item.UnitOfMeasure)) return ConsumableStockStatus.ToVerify;
        var ordered = OrderFor(item.Id).IsOpen;
        if (latest.Quantity < item.MinimumStock.Value) return ordered ? ConsumableStockStatus.BelowMinimumOrdered : ConsumableStockStatus.ToOrder;
        return ordered ? ConsumableStockStatus.Ordered : ConsumableStockStatus.Ok;
    }

    public int ItemsToOrder => Items.Count(item => item.IsActive && StatusFor(item) == ConsumableStockStatus.ToOrder);
    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
    public bool HasImported(string fingerprint) => ImportedFingerprints.Contains(fingerprint);
    public void MarkImported(string fingerprint) { if (!string.IsNullOrWhiteSpace(fingerprint)) ImportedFingerprints.Add(fingerprint); }
    public string NextCode()
    {
        var maximum = Items.Select(item => item.InternalCode).Where(code => code.StartsWith("CON-", StringComparison.OrdinalIgnoreCase))
            .Select(code => int.TryParse(code.AsSpan(4), out var value) ? value : 0).DefaultIfEmpty().Max();
        return $"CON-{maximum + 1:000}";
    }
}
