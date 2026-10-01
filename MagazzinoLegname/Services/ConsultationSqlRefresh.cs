using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Services;

// UI-thread refresh boundary: subscribers must not render partially reloaded sources.
public static class ConsultationSqlRefresh
{
    public static bool IsRefreshing { get; private set; }
    public static event Action? Starting;
    public static void Run(Action reload)
    {
        if (IsRefreshing) throw new InvalidOperationException("Aggiornamento SQL già in corso.");
        IsRefreshing = true;
        try { Starting?.Invoke(); reload(); }
        finally { IsRefreshing = false; }
    }

    public static void Reload()
    {
        MaterialParametersService.Shared.Reload();
        SupplierCatalogService.Shared.Reload();
        var workflow = ClassificationWorkflowService.Shared;
        workflow.ReloadInboundLoads(); // Includes groups, packages and waste adjustments.
        InventoryProjectionService.Shared.ReloadSqlTerminalMovements(); // Includes returns.
        LegacyHistoricalStore.Shared.Reload();
        using var db = SqlPersistenceRoot.ContextFactory.CreateDbContext();
        var movements = db.ClassificationMovements.AsNoTracking()
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id).ToArray();
        workflow.ClassificationHistory.Clear();
        foreach (var row in movements)
            workflow.ClassificationHistory.Add(new ClassificationMovement {
                MovementId = row.Id, LoadId = row.LoadId, MaterialGroupId = row.MaterialGroupId,
                ClassificationDate = DateTime.SpecifyKind(row.OccurredAtUtc, DateTimeKind.Utc).ToLocalTime(),
                ClassificationOperator = row.OperatorSnapshot });
    }
}
