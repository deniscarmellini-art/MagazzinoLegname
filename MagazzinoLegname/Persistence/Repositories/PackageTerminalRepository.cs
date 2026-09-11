using System.Data;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using MagazzinoLegname.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence.Repositories;

public sealed record SqlPackageTerminalState(bool Exists, PackageType PackageType,
    PackageTerminalState? TerminalState);

public sealed record PersistedTerminalMovements(IReadOnlyList<MaterialDischargeMovement> Discharges,
    IReadOnlyList<SupplementaryPackageExitMovement> SupplementaryExits);

public interface IPackageTerminalRepository
{
    SqlPackageTerminalState FindState(string packageCode);
    PackageExitResult Discharge(string packageCode, string operatorName);
    PersistedTerminalMovements GetAllDischarges();
}

public sealed class SqlPackageTerminalRepository(IDbContextFactory<MagazzinoDbContext> contextFactory)
    : IPackageTerminalRepository
{
    public SqlPackageTerminalState FindState(string packageCode)
    {
        using var db = contextFactory.CreateDbContext();
        var package = db.Packages.AsNoTracking().Include(x => x.TerminalEvent)
            .SingleOrDefault(x => x.PackageCode == packageCode);
        return package is null
            ? new(false, PackageType.Official, null)
            : new(true, ToModel(package.PackageType), ToTerminalState(package.TerminalEvent?.EventType));
    }

    public PackageExitResult Discharge(string packageCode, string operatorName)
    {
        using var strategyContext = contextFactory.CreateDbContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();
        PackageExitResult? result = null;
        try
        {
            strategy.Execute(() =>
            {
                using var db = contextFactory.CreateDbContext();
                using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            var package = db.Packages
                .FromSqlInterpolated($"SELECT * FROM dbo.Packages WITH (UPDLOCK, HOLDLOCK) WHERE PackageCode = {packageCode}")
                .Include(x => x.MaterialGroup).Include(x => x.Load).ThenInclude(x => x.Supplier)
                .SingleOrDefault() ?? throw new InvalidOperationException("Pacco non trovato.");
            if (db.PackageTerminalEvents.Any(x => x.PackageId == package.Id))
                throw new PackageAlreadyExitedException(package.PackageCode);
            var operatorItem = db.Operators.SingleOrDefault(x => x.IsActive &&
                (x.FirstName + " " + x.LastName) == operatorName)
                ?? throw new InvalidOperationException("L'operatore selezionato non è disponibile nel database.");

            var supplementary = package.PackageType == PersistentPackageType.Supplementary;
            if (!supplementary && (!package.MaterialGroup.IsClassified || !package.MaterialGroup.WasteVerified))
                throw new InvalidOperationException("È necessario completare classificazione e rettifica scarti prima dello scarico.");
            var currentInventoryCubicMeters = supplementary ? 0m : CalculateCurrentPackageCubicMeters(db, package);
            var occurredAt = DateTime.Now;
            var terminal = new PackageTerminalEventEntity
            {
                Id = Guid.NewGuid(), PackageId = package.Id,
                EventType = supplementary ? PackageTerminalEventType.SupplementaryExit : PackageTerminalEventType.Discharge,
                OccurredAtUtc = occurredAt.ToUniversalTime(), OperatorId = operatorItem.Id,
                OperatorSnapshot = operatorItem.FirstName + " " + operatorItem.LastName,
                InventoryCubicMeters = supplementary ? null : currentInventoryCubicMeters
            };
            package.Status = supplementary ? "Uscita supplementare" : "Scaricato";
            db.PackageTerminalEvents.Add(terminal);
            db.SaveChanges();
            transaction.Commit();
                result = new PackageExitResult(package.PackageCode, ToModel(package.PackageType), occurredAt,
                    terminal.OperatorSnapshot, terminal.InventoryCubicMeters,
                    supplementary ? "Uscita supplementare registrata senza movimento di MC."
                        : $"Pacco {package.PackageCode} scaricato correttamente · MC scaricati: {currentInventoryCubicMeters:N6}");
            });
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new PackageAlreadyExitedException(packageCode);
        }
        return result ?? throw new InvalidOperationException("Lo scarico non è stato registrato.");
    }

    private static decimal CalculateCurrentPackageCubicMeters(MagazzinoDbContext db, PackageEntity package)
    {
        var adjustment = db.WasteAdjustments.AsNoTracking().Single(x => x.MaterialGroupId == package.MaterialGroupId);
        var alreadyDischarged = db.PackageTerminalEvents.AsNoTracking()
            .Where(x => x.EventType == PackageTerminalEventType.Discharge &&
                x.Package.MaterialGroupId == package.MaterialGroupId)
            .Sum(x => x.InventoryCubicMeters ?? 0m);
        var presentPackageIds = db.Packages.AsNoTracking()
            .Where(x => x.MaterialGroupId == package.MaterialGroupId &&
                x.PackageType == PersistentPackageType.Official && x.TerminalEvent == null)
            .OrderBy(x => x.SequenceNumber).Select(x => x.Id).ToList();
        var packageIndex = presentPackageIds.IndexOf(package.Id);
        if (packageIndex < 0) throw new PackageAlreadyExitedException(package.PackageCode);
        var residual = Math.Max(0m, adjustment.RealAvailableCubicMeters - alreadyDischarged);
        return InventoryProjectionService.DistributeExactly(residual, presentPackageIds.Count)[packageIndex];
    }

    public PersistedTerminalMovements GetAllDischarges()
    {
        using var db = contextFactory.CreateDbContext();
        var rows = db.PackageTerminalEvents.AsNoTracking().Include(x => x.Package)
            .ThenInclude(x => x.Load).ThenInclude(x => x.Supplier)
            .Where(x => x.EventType == PackageTerminalEventType.Discharge ||
                x.EventType == PackageTerminalEventType.SupplementaryExit).ToList();
        return new(
            rows.Where(x => x.EventType == PackageTerminalEventType.Discharge).Select(x => new MaterialDischargeMovement
            {
                MovementId = x.Id, PackageId = x.PackageId, PackageCode = x.Package.PackageCode,
                LoadId = x.Package.LoadId, MaterialGroupId = x.Package.MaterialGroupId,
                LoadNumber = x.Package.Load.LoadNumber, SupplierName = x.Package.Load.Supplier.Name,
                DischargeDate = x.OccurredAtUtc.ToLocalTime(), DischargeOperator = x.OperatorSnapshot,
                DischargedCubicMeters = x.InventoryCubicMeters ?? 0m, PreviousStatus = "Presente", NextStatus = "Scaricato"
            }).ToList(),
            rows.Where(x => x.EventType == PackageTerminalEventType.SupplementaryExit).Select(x => new SupplementaryPackageExitMovement
            {
                MovementId = x.Id, PackageId = x.PackageId, PackageCode = x.Package.PackageCode,
                LoadId = x.Package.LoadId, MaterialGroupId = x.Package.MaterialGroupId,
                LoadNumber = x.Package.Load.LoadNumber, SupplierName = x.Package.Load.Supplier.Name,
                ExitDate = x.OccurredAtUtc.ToLocalTime(), ExitOperator = x.OperatorSnapshot
            }).ToList());
    }

    private static PackageType ToModel(PersistentPackageType type) => type == PersistentPackageType.Supplementary
        ? PackageType.Supplementary : PackageType.Official;
    private static PackageTerminalState? ToTerminalState(PackageTerminalEventType? type) => type switch
    {
        PackageTerminalEventType.Discharge => PackageTerminalState.Discharged,
        PackageTerminalEventType.Return => PackageTerminalState.Returned,
        PackageTerminalEventType.ManualRemoval => PackageTerminalState.ManuallyRemoved,
        PackageTerminalEventType.SupplementaryExit => PackageTerminalState.SupplementaryExited,
        _ => null
    };
}

public sealed class PackageAlreadyExitedException(string packageCode)
    : InvalidOperationException($"Pacco già scaricato. Il pacco {packageCode} risulta già uscito dalla giacenza.");
