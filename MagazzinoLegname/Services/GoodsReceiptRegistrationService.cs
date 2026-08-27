using MagazzinoLegname.Models;

namespace MagazzinoLegname.Services;

public sealed class GoodsReceiptRegistrationService
{
    private readonly object _registrationLock = new();
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;

    public ClassificationLoad Register(GoodsReceiptLoadDraft draft, Supplier supplier,
        string receiptOperator, DateTime arrivalDate, IEnumerable<GoodsReceiptLine> lines,
        IReadOnlyList<PhysicalPackageDraft> packages)
    {
        lock (_registrationLock)
        {
            var groups = lines.Select(line => new MaterialGroupClassification
            {
                GroupId = line.GroupId, LoadId = draft.Id,
                IncomingThickness = line.IncomingThickness,
                ConventionalThickness = line.ConventionalThickness,
                UsefulThickness = line.UsefulProductionThickness,
                IncomingWidth = line.IncomingWidth,
                WidthAfterPlaning = line.WidthAfterPlaning,
                FinalWidth = line.FinalWidth,
                IncomingLength = line.IncomingLength,
                FinalLength = line.FinalLength,
                Quality = line.Quality,
                PackageCount = line.PackageCount,
                InitialPieces = line.EnteredPieces,
                AppliedPrice = line.PrezzoApplicato,
                LineValue = line.LineValue
            }).ToList();
            var load = new ClassificationLoad(groups)
            {
                Id = draft.Id, LoadNumber = draft.LoadNumber,
                SupplierId = supplier.Id, LoadYear = draft.Year, AnnualProgressive = draft.AnnualSequence,
                SupplierName = supplier.Name, SupplierCode = supplier.Code,
                Certification = draft.CertificationApplied,
                ArrivalDate = arrivalDate.Date,
                DeliveryNoteNumber = draft.DeliveryNoteNumber,
                ReceiptOperator = receiptOperator
            };
            _workflow.RegisterLoad(load, packages);
            return load;
        }
    }
}
