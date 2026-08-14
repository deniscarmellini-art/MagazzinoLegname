namespace MagazzinoLegname.Models;

public enum PackageLookupStatus
{
    Ready,
    InvalidQr,
    NotFound,
    NotClassified,
    WasteAdjustmentRequired,
    AlreadyDischarged
}

public sealed record PackageLookupResult(PackageLookupStatus Status, string Message,
    InventoryPackage? Package = null, MaterialDischargeMovement? PreviousMovement = null)
{
    public bool CanDischarge => Status == PackageLookupStatus.Ready;
}
