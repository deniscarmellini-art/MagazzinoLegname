using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;

namespace MagazzinoLegname.Services;

public sealed class GoodsReceiptRegistrationService
{
    private readonly ClassificationWorkflowService _workflow = ClassificationWorkflowService.Shared;

    public ClassificationLoad Register(GoodsReceiptLoadDraft draft, Supplier supplier,
        string receiptOperator, DateTime arrivalDate, int expectedPackages, IReadOnlyList<GoodsReceiptLine> lines)
    {
        var operatorItem = OperatorCatalogService.Shared.Operators.SingleOrDefault(x =>
            x.IsActive && x.DisplayName.Equals(receiptOperator, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("L'operatore selezionato non è disponibile nel database.");
        try
        {
            var persisted = SqlPersistenceRoot.InboundLoads.Register(draft, supplier, operatorItem,
                arrivalDate, expectedPackages, lines);
            _workflow.RegisterPersistedLoad(persisted.Load, persisted.Packages);
            return persisted.Load;
        }
        catch (Exception exception) { throw SqlPersistenceRoot.OperatorException(exception); }
    }
}
