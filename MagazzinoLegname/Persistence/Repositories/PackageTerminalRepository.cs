using System.Data;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence.Repositories;

public sealed record SqlPackageTerminalState(bool Exists, PackageType PackageType,
    PackageTerminalState? TerminalState);

public sealed record PersistedTerminalMovements(IReadOnlyList<MaterialDischargeMovement> Discharges,
    IReadOnlyList<SupplementaryPackageExitMovement> SupplementaryExits,
    IReadOnlyList<ManualPackageRemovalMovement> ManualRemovals,
    IReadOnlyList<SupplierReturnMovement> SupplierReturns);

public interface IPackageTerminalRepository
{
    SqlPackageTerminalState FindState(string packageCode);
    PackageExitResult Discharge(string packageCode, string operatorName);
    ManualPackageRemovalMovement Remove(string packageCode, string operatorName, string reason, string? note);
    SupplierReturnResult Return(Guid loadId, IReadOnlyCollection<string> packageCodes, SupplierReturnMode mode,
        string operatorName, string reason, string? note, string? documentReference);
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
        return Services.InventoryProjectionService.DistributeExactly(residual, presentPackageIds.Count)[packageIndex];
    }

    public ManualPackageRemovalMovement Remove(string packageCode, string operatorName, string reason, string? note)
    {
        ManualPackageRemovalMovement? result = null;
        ExecuteTerminalTransaction(packageCode, operatorName, (db, package, operatorItem) =>
        {
            var supplementary = package.PackageType == PersistentPackageType.Supplementary;
            var removedCubicMeters = supplementary ? (decimal?)null : CalculateCurrentPackageCubicMeters(db, package);
            var occurredAt = DateTime.Now;
            var terminal = new PackageTerminalEventEntity
            {
                Id = Guid.NewGuid(), PackageId = package.Id, EventType = PackageTerminalEventType.ManualRemoval,
                OccurredAtUtc = occurredAt.ToUniversalTime(), OperatorId = operatorItem.Id,
                OperatorSnapshot = DisplayName(operatorItem), InventoryCubicMeters = removedCubicMeters,
                Reason = reason.Trim(), Note = NullIfEmpty(note)
            };
            package.Status = "Rimosso manualmente";
            db.PackageTerminalEvents.Add(terminal);
            result = new ManualPackageRemovalMovement
            {
                MovementId = terminal.Id, PackageId = package.Id, PackageCode = package.PackageCode,
                LoadId = package.LoadId, MaterialGroupId = package.MaterialGroupId,
                LoadNumber = package.Load.LoadNumber, SupplierName = package.Load.Supplier.Name,
                RemovalDate = occurredAt, RemovalOperator = terminal.OperatorSnapshot,
                RemovedCubicMeters = removedCubicMeters, Reason = terminal.Reason, Note = terminal.Note ?? string.Empty
            };
        });
        return result ?? throw new InvalidOperationException("La rimozione non è stata registrata.");
    }

    public SupplierReturnResult Return(Guid loadId, IReadOnlyCollection<string> packageCodes, SupplierReturnMode mode,
        string operatorName, string reason, string? note, string? documentReference)
    {
        using var strategyContext = contextFactory.CreateDbContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();
        SupplierReturnResult? result = null;
        try
        {
            strategy.Execute(() =>
            {
                using var db = contextFactory.CreateDbContext();
                using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
                var operatorItem = ActiveOperator(db, operatorName);
                var packages = new List<PackageEntity>();
                foreach (var code in packageCodes.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var package = LockedPackage(db, code);
                    if (package.LoadId != loadId) throw new InvalidOperationException("Uno o più pacchi non appartengono al carico selezionato.");
                    EnsureAvailable(db, package);
                    packages.Add(package);
                }
                if (packages.Count == 0) throw new InvalidOperationException("Selezionare almeno un pacco da rendere.");
                if (packages.Any(x => x.PackageType == PersistentPackageType.Supplementary))
                    throw new InvalidOperationException("I pacchi supplementari non possono generare movimenti di reso con MC.");

                var occurredAt = DateTime.Now;
                var operation = new SupplierReturnOperationEntity
                {
                    Id = Guid.NewGuid(), LoadId = loadId, OccurredAtUtc = occurredAt.ToUniversalTime(),
                    OperatorId = operatorItem.Id, OperatorSnapshot = DisplayName(operatorItem),
                    Reason = reason.Trim(), Note = NullIfEmpty(note), DocumentReference = NullIfEmpty(documentReference)
                };
                decimal physical = 0m, inventory = 0m;
                foreach (var package in packages)
                {
                    var current = CalculateCurrentPackageCubicMeters(db, package);
                    physical += package.IncomingPhysicalCubicMeters;
                    inventory += current;
                    var terminal = new PackageTerminalEventEntity
                    {
                        Id = Guid.NewGuid(), PackageId = package.Id, EventType = PackageTerminalEventType.Return,
                        OccurredAtUtc = operation.OccurredAtUtc, OperatorId = operatorItem.Id,
                        OperatorSnapshot = operation.OperatorSnapshot, ReturnOperationId = operation.Id,
                        InventoryCubicMeters = current, ReturnedPhysicalCubicMeters = package.IncomingPhysicalCubicMeters,
                        Reason = operation.Reason, Note = operation.Note
                    };
                    operation.PackageEvents.Add(terminal);
                    package.Status = "Reso";
                }
                db.SupplierReturnOperations.Add(operation);
                db.SaveChanges();
                transaction.Commit();
                result = new(operation.Id, mode, packages.Count, physical, inventory);
            });
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new PackageAlreadyExitedException(packageCodes.FirstOrDefault() ?? string.Empty);
        }
        return result ?? throw new InvalidOperationException("Il reso non è stato registrato.");
    }

    private void ExecuteTerminalTransaction(string packageCode, string operatorName,
        Action<MagazzinoDbContext, PackageEntity, OperatorEntity> prepare)
    {
        using var strategyContext = contextFactory.CreateDbContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();
        try
        {
            strategy.Execute(() =>
            {
                using var db = contextFactory.CreateDbContext();
                using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
                var package = LockedPackage(db, packageCode);
                EnsureAvailable(db, package);
                prepare(db, package, ActiveOperator(db, operatorName));
                db.SaveChanges();
                transaction.Commit();
            });
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new PackageAlreadyExitedException(packageCode);
        }
    }

    private static PackageEntity LockedPackage(MagazzinoDbContext db, string packageCode) => db.Packages
        .FromSqlInterpolated($"SELECT * FROM dbo.Packages WITH (UPDLOCK, HOLDLOCK) WHERE PackageCode = {packageCode}")
        .Include(x => x.MaterialGroup).Include(x => x.Load).ThenInclude(x => x.Supplier)
        .SingleOrDefault() ?? throw new InvalidOperationException("Pacco non trovato.");

    private static void EnsureAvailable(MagazzinoDbContext db, PackageEntity package)
    {
        if (db.PackageTerminalEvents.Any(x => x.PackageId == package.Id))
            throw new PackageAlreadyExitedException(package.PackageCode);
    }

    private static OperatorEntity ActiveOperator(MagazzinoDbContext db, string operatorName) =>
        db.Operators.SingleOrDefault(x => x.IsActive && (x.FirstName + " " + x.LastName) == operatorName)
        ?? throw new InvalidOperationException("L'operatore selezionato non è disponibile nel database.");

    private static string DisplayName(OperatorEntity item) => $"{item.FirstName} {item.LastName}";
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public PersistedTerminalMovements GetAllDischarges()
    {
        using var db = contextFactory.CreateDbContext();
        var rows = db.PackageTerminalEvents.AsNoTracking().Include(x => x.ReturnOperation).Include(x => x.Package)
            .ThenInclude(x => x.Load).ThenInclude(x => x.Supplier)
            .ToList();
        var returnModes = rows.Where(x => x.EventType == PackageTerminalEventType.Return && x.ReturnOperationId.HasValue)
            .GroupBy(x => x.ReturnOperationId!.Value).ToDictionary(group => group.Key, group =>
                db.Packages.AsNoTracking().Any(package => package.LoadId == group.First().Package.LoadId &&
                    package.PackageType == PersistentPackageType.Official && package.TerminalEvent == null)
                    ? SupplierReturnMode.Partial : SupplierReturnMode.Total);
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
            }).ToList(),
            rows.Where(x => x.EventType == PackageTerminalEventType.ManualRemoval).Select(x => new ManualPackageRemovalMovement
            {
                MovementId = x.Id, PackageId = x.PackageId, PackageCode = x.Package.PackageCode,
                LoadId = x.Package.LoadId, MaterialGroupId = x.Package.MaterialGroupId,
                LoadNumber = x.Package.Load.LoadNumber, SupplierName = x.Package.Load.Supplier.Name,
                RemovalDate = x.OccurredAtUtc.ToLocalTime(), RemovalOperator = x.OperatorSnapshot,
                RemovedCubicMeters = x.InventoryCubicMeters, Reason = x.Reason ?? string.Empty, Note = x.Note ?? string.Empty
            }).ToList(),
            rows.Where(x => x.EventType == PackageTerminalEventType.Return).Select(x => new SupplierReturnMovement
            {
                MovementId = x.Id, ReturnOperationId = x.ReturnOperationId!.Value,
                PackageId = x.PackageId, PackageCode = x.Package.PackageCode,
                LoadId = x.Package.LoadId, MaterialGroupId = x.Package.MaterialGroupId,
                LoadNumber = x.Package.Load.LoadNumber, SupplierName = x.Package.Load.Supplier.Name,
                ReturnDate = x.OccurredAtUtc.ToLocalTime(), ReturnOperator = x.OperatorSnapshot,
                Reason = x.Reason ?? x.ReturnOperation?.Reason ?? string.Empty,
                Note = x.Note ?? x.ReturnOperation?.Note ?? string.Empty,
                DocumentReference = x.ReturnOperation?.DocumentReference ?? string.Empty,
                ReturnedPhysicalCubicMeters = x.ReturnedPhysicalCubicMeters ?? 0m,
                RemovedInventoryCubicMeters = x.InventoryCubicMeters ?? 0m,
                Mode = returnModes[x.ReturnOperationId!.Value]
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
