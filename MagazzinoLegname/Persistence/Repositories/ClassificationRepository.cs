using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MagazzinoLegname.Persistence.Repositories;

public sealed record PersistedClassificationState(byte[] RowVersion, bool IsClassified,
    DateTime? ClassificationDate, string? ClassificationOperator,
    DateTime? OfficialLabelsPrintedAt, string? OfficialLabelsPrintedBy);

public interface IClassificationRepository
{
    PersistedClassificationState MarkOfficialLabelsPrinted(Guid loadId, Guid materialGroupId, byte[] rowVersion,
        Operator operatorItem, DateTime printedAt);
    PersistedClassificationState MarkClassified(Guid loadId, Guid materialGroupId, byte[] rowVersion,
        Operator operatorItem, DateTime classifiedAt);
    PersistedClassificationState UndoClassification(Guid loadId, Guid materialGroupId, byte[] rowVersion);
}

public sealed class SqlClassificationRepository(IDbContextFactory<MagazzinoDbContext> contextFactory)
    : IClassificationRepository
{
    public PersistedClassificationState MarkOfficialLabelsPrinted(Guid loadId, Guid materialGroupId, byte[] rowVersion,
        Operator operatorItem, DateTime printedAt)
    {
        using var db = contextFactory.CreateDbContext();
        var entity = LoadCurrentGroup(db, loadId, materialGroupId, rowVersion);
        EnsureActiveOperator(db, operatorItem);
        entity.OfficialLabelsPrintedAt = printedAt;
        entity.OfficialLabelsPrintedBy = operatorItem.DisplayName;
        SaveChanges(db, entity, "MarkOfficialLabelsPrinted");
        return State(entity);
    }

    public PersistedClassificationState MarkClassified(Guid loadId, Guid materialGroupId, byte[] rowVersion,
        Operator operatorItem, DateTime classifiedAt)
    {
        using var db = contextFactory.CreateDbContext();
        var entity = LoadCurrentGroup(db, loadId, materialGroupId, rowVersion);
        EnsureActiveOperator(db, operatorItem);
        if (entity.IsClassified) return State(entity);
        entity.IsClassified = true;
        entity.WasteVerified = false;
        var movement = new ClassificationMovementEntity
        {
            Id = Guid.NewGuid(), LoadId = entity.LoadId, MaterialGroupId = entity.Id,
            OccurredAtUtc = classifiedAt.ToUniversalTime(), OperatorId = operatorItem.Id,
            OperatorSnapshot = operatorItem.DisplayName
        };
        // The Guid is assigned by the application. Adding this object through an already
        // tracked graph can make EF infer that it is an existing child. Register it explicitly
        // as a new row so SaveChanges emits INSERT, never UPDATE.
        db.ClassificationMovements.Add(movement);
        PersistenceDebugLog.Write($"MarkClassified tracking: MaterialGroupEntity={db.Entry(entity).State}; " +
            $"ClassificationMovementEntity={db.Entry(movement).State}.");
        SaveChanges(db, entity, "MarkClassified");
        return State(entity);
    }

    public PersistedClassificationState UndoClassification(Guid loadId, Guid materialGroupId, byte[] rowVersion)
    {
        using var db = contextFactory.CreateDbContext();
        var entity = LoadCurrentGroup(db, loadId, materialGroupId, rowVersion);
        if (entity.WasteVerified)
            throw new InvalidOperationException("La classificazione non può essere annullata dopo la Rettifica scarti.");
        entity.IsClassified = false;
        SaveChanges(db, entity, "UndoClassification");
        return State(entity);
    }

    private static MaterialGroupEntity LoadCurrentGroup(MagazzinoDbContext db, Guid loadId,
        Guid materialGroupId, byte[] expectedRowVersion)
    {
        var entity = db.MaterialGroups.Include(x => x.ClassificationMovements)
            .SingleOrDefault(x => x.Id == materialGroupId && x.LoadId == loadId)
            ?? throw new InvalidOperationException("Il gruppo materiale non è più presente nel database.");
        // The value read by the UI is the concurrency token. EF must use that value in the
        // UPDATE predicate; the freshly queried database value must not silently replace it.
        if (expectedRowVersion.Length > 0)
            db.Entry(entity).Property(x => x.RowVersion).OriginalValue = expectedRowVersion.ToArray();
        var entry = db.Entry(entity);
        var rowVersion = entry.Property(x => x.RowVersion);
        PersistenceDebugLog.Write($"Classification pre-UPDATE: LoadId UI={loadId}; MaterialGroupId UI={materialGroupId}; " +
            $"UI RowVersion={FormatRowVersion(expectedRowVersion)}; LoadId DB={entity.LoadId}; " +
            $"MaterialGroupId DB={entity.Id}; DB RowVersion={FormatRowVersion(entity.RowVersion)}; " +
            $"EF State={entry.State}; EF Original={FormatRowVersion(rowVersion.OriginalValue)}; " +
            $"EF Current={FormatRowVersion(rowVersion.CurrentValue)}.");
        var concurrencyProperties = entry.Metadata.GetProperties().Where(property => property.IsConcurrencyToken)
            .Select(property => $"{property.Name}={FormatValue(entry.Property(property.Name).CurrentValue)}");
        PersistenceDebugLog.Write("MaterialGroupEntity concurrency tokens: " + string.Join("; ", concurrencyProperties));
        return entity;
    }

    private static void SaveChanges(MagazzinoDbContext db, MaterialGroupEntity entity, string operation)
    {
        var entry = db.Entry(entity);
        var rowVersion = entry.Property(x => x.RowVersion);
        PersistenceDebugLog.Write($"{operation}: SaveChanges #1; EF State={entry.State}; " +
            $"EF Original={FormatRowVersion(rowVersion.OriginalValue)}; EF Current={FormatRowVersion(rowVersion.CurrentValue)}.");
        try
        {
            db.SaveChanges();
            PersistenceDebugLog.Write($"{operation}: SaveChanges completato; nuova RowVersion=" +
                $"{FormatRowVersion(entity.RowVersion)}; EF State={entry.State}.");
        }
        catch (DbUpdateConcurrencyException exception)
        {
            PersistenceDebugLog.WriteException(operation, exception);
            PersistenceDebugLog.Write($"{operation}: SaveChanges fallito; EF State={entry.State}; " +
                $"EF Original={FormatRowVersion(rowVersion.OriginalValue)}; EF Current={FormatRowVersion(rowVersion.CurrentValue)}.");
            LogConcurrencyEntries(exception);
            throw;
        }
        catch (Exception exception)
        {
            PersistenceDebugLog.WriteException(operation, exception);
            throw;
        }
    }

    private static void LogConcurrencyEntries(DbUpdateConcurrencyException exception)
    {
        PersistenceDebugLog.Write($"DbUpdateConcurrencyException.Entries count={exception.Entries.Count}.");
        foreach (var conflictEntry in exception.Entries)
        {
            var primaryKey = conflictEntry.Metadata.FindPrimaryKey()?.Properties
                .Select(property => $"{property.Name}={FormatValue(conflictEntry.Property(property.Name).CurrentValue)}")
                ?? [];
            PersistenceDebugLog.Write($"Conflict Entry: EntityType={conflictEntry.Metadata.ClrType.FullName}; " +
                $"State={conflictEntry.State}; PrimaryKey=[{string.Join(", ", primaryKey)}].");
            foreach (var property in conflictEntry.Metadata.GetProperties())
            {
                var propertyEntry = conflictEntry.Property(property.Name);
                PersistenceDebugLog.Write($"  {property.Name}: Original={FormatValue(propertyEntry.OriginalValue)}; " +
                    $"Current={FormatValue(propertyEntry.CurrentValue)}; IsConcurrencyToken={property.IsConcurrencyToken}.");
            }
        }
    }

    private static string FormatRowVersion(byte[] value) => value.Length == 0
        ? "<empty>" : $"0x{Convert.ToHexString(value)}";

    private static string FormatValue(object? value) => value switch
    {
        null => "<null>",
        byte[] bytes => FormatRowVersion(bytes),
        DateTime dateTime => dateTime.ToString("O"),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"
    };

    private static void EnsureActiveOperator(MagazzinoDbContext db, Operator operatorItem)
    {
        if (!db.Operators.Any(x => x.Id == operatorItem.Id && x.IsActive))
            throw new InvalidOperationException("L'operatore selezionato non è più disponibile nel database.");
    }

    private static PersistedClassificationState State(MaterialGroupEntity entity)
    {
        var movement = entity.ClassificationMovements.OrderByDescending(x => x.OccurredAtUtc).FirstOrDefault();
        return new PersistedClassificationState(entity.RowVersion, entity.IsClassified,
            entity.IsClassified ? movement?.OccurredAtUtc.ToLocalTime() : null,
            entity.IsClassified ? movement?.OperatorSnapshot : null,
            entity.OfficialLabelsPrintedAt, entity.OfficialLabelsPrintedBy);
    }
}
