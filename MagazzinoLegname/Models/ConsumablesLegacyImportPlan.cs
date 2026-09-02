namespace MagazzinoLegname.Models;

public sealed record ConsumablesLegacyImportPlan(
    IReadOnlyList<ConsumableItem> Items,
    IReadOnlyList<ConsumableInventoryReading> Readings,
    IReadOnlyList<ConsumableOrderInfo> Orders,
    IReadOnlyList<string> Warnings,
    int EmbeddedImages,
    int AssociatedImages,
    string FileFingerprint = "",
    IReadOnlyList<DateTime>? StockDates = null,
    IReadOnlyList<ConsumableLegacyPhoto>? Photos = null,
    int NewItems = 0,
    int ExistingItems = 0,
    int ItemsToVerify = 0,
    int AmbiguousItems = 0)
{
    public int ItemCount => Items.Count;
    public int ReadingCount => Readings.Count;
    public int UnassociatedImages => EmbeddedImages - AssociatedImages;
    public int StockDateCount => StockDates?.Count ?? 0;
    public int UncertainImages => Photos?.Count(photo => photo.Status == ConsumableLegacyPhotoStatus.Uncertain) ?? 0;
    public bool CanImport => Items.Count > 0 && AmbiguousItems == 0;
    public string Summary => $"Articoli: {ItemCount:N0} (nuovi {NewItems:N0}, esistenti {ExistingItems:N0}) · Date STOCK: {StockDateCount:N0} · Rilevazioni: {ReadingCount:N0} · Da verificare: {ItemsToVerify:N0} · Immagini: {EmbeddedImages:N0} (associate: {AssociatedImages:N0})";
}

public enum ConsumableLegacyPhotoStatus { Associated, Unassociated, Uncertain }
public sealed record ConsumableLegacyPhoto(string FileName, string Description, byte[] Content, string Extension,
    Guid? MaterialId, string? ProductName, ConsumableLegacyPhotoStatus Status, string Detail);

public sealed record ConsumablesLegacyImportResult(int Items, int Readings, int Orders, int Photos = 0,
    int UnassociatedPhotos = 0, DateTime? FirstReading = null, DateTime? LastReading = null, int ItemsToVerify = 0);
