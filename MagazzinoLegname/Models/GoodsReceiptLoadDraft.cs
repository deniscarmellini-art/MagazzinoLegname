namespace MagazzinoLegname.Models;

public sealed class GoodsReceiptLoadDraft
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SupplierId { get; private set; }
    public string SupplierCodeApplied { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public int AnnualSequence { get; private set; }
    public string LoadNumber { get; private set; } = string.Empty;
    public string DeliveryNoteNumber { get; set; } = string.Empty;
    public string CertificationApplied { get; private set; } = string.Empty;
    public bool IsCertificationCaptured { get; private set; }
    public bool IsNumberAssigned => AnnualSequence > 0;

    public void AssignNumber(LoadNumberAssignment assignment, string supplierCode)
    {
        if (IsNumberAssigned) return;
        SupplierId = assignment.SupplierId; Year = assignment.Year;
        AnnualSequence = assignment.AnnualSequence; LoadNumber = assignment.LoadNumber;
        SupplierCodeApplied = supplierCode;
    }
    public void CaptureCertification(string certification)
    {
        if (IsCertificationCaptured) return;
        CertificationApplied = certification; IsCertificationCaptured = true;
    }
}
