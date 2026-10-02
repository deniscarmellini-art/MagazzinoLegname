using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence;

public enum DatabaseFailureKind { Configuration, Unavailable, Timeout, ConcurrencyConflict, UniqueConstraint, Unknown, WindowsLoginDenied, DatabaseUnavailable, SchemaOutdated, MaintenanceBusy }
public sealed record DatabaseFailure(DatabaseFailureKind Kind, string OperatorMessage);

public static class DatabaseErrorTranslator
{
    public static DatabaseFailure Translate(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DatabaseConfigurationException) return new(DatabaseFailureKind.Configuration, current.Message);
            if (current is DatabaseSchemaException) return new(DatabaseFailureKind.SchemaOutdated, current.Message);
            if (current is DbUpdateConcurrencyException) return new(DatabaseFailureKind.ConcurrencyConflict, "I dati sono stati modificati da un'altra postazione. Aggiornare la pagina e riprovare.");
            if (current is SqlException sql) return FromSqlNumber(sql.Number);
            if (current is TimeoutException) return FromSqlNumber(-2);
        }
        return new(DatabaseFailureKind.Unknown, "Operazione non completata. Contattare l'amministratore del sistema.");
    }

    // Separate numeric mapping makes diagnostic coverage possible without manufacturing SqlException instances.
    public static DatabaseFailure FromSqlNumber(int number) => number switch
    {
        2601 or 2627 => new(DatabaseFailureKind.UniqueConstraint, "L'operazione è già stata registrata oppure utilizza un codice già esistente."),
        -2 or 121 or 258 => new(DatabaseFailureKind.Timeout, "Timeout della connessione o della risposta SQL. Verificare disponibilità del server e rete; riprovare."),
        18456 or 18452 or 18470 => new(DatabaseFailureKind.WindowsLoginDenied, "Accesso Windows non autorizzato a SQL Server. Richiedere all'amministratore l'abilitazione dell'utente o del gruppo di dominio."),
        911 => new(DatabaseFailureKind.DatabaseUnavailable, "Il database configurato non esiste sul server selezionato. Nessun database è stato creato."),
        4060 => new(DatabaseFailureKind.DatabaseUnavailable, "Impossibile aprire il database configurato: potrebbe non esistere oppure l'utente Windows non dispone dell'accesso. L'amministratore deve verificare nome e permessi; nessun database è stato creato."),
        51001 => new(DatabaseFailureKind.MaintenanceBusy, "Un'altra postazione sta aggiornando lo schema. Attendere il completamento e riprovare."),
        2 or 26 or 40 or 53 or 64 or 67 or 233 or 10060 or 10061 or 11001 => new(DatabaseFailureKind.Unavailable, "Server SQL non raggiungibile. Verificare nome, istanza/porta e rete; TCP potrebbe non essere abilitato."),
        _ => new(DatabaseFailureKind.Unavailable, "Connessione SQL non riuscita. Richiedere una verifica di rete, autorizzazioni e certificato del server.")
    };
}

public sealed class DatabaseHealthService(IDbContextFactory<MagazzinoDbContext> contextFactory)
{
    // Read-only: never calls Migrate, including when the maintenance environment variable is set.
    public async Task<DatabaseFailure?> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await context.Database.OpenConnectionAsync(cancellationToken);
            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            var known = context.Database.GetMigrations().ToArray();
            if (known.Except(applied).Any() || applied.Except(known).Any())
                return new(DatabaseFailureKind.SchemaOutdated, "Schema non compatibile con questa versione. Richiedere l'aggiornamento dalla postazione autorizzata; il controllo diagnostico non modifica lo schema.");
            return null;
        }
        catch (Exception exception) { PersistenceDebugLog.WriteException("Database health check", exception); return DatabaseErrorTranslator.Translate(exception); }
    }
}
