using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence;

public enum DatabaseFailureKind { Configuration, Unavailable, Timeout, ConcurrencyConflict, UniqueConstraint, Unknown }
public sealed record DatabaseFailure(DatabaseFailureKind Kind, string OperatorMessage);

public static class DatabaseErrorTranslator
{
    public static DatabaseFailure Translate(Exception exception)
    {
        var root = Unwrap(exception);
        return root switch
        {
            DatabaseConfigurationException => new(DatabaseFailureKind.Configuration, root.Message),
            DbUpdateConcurrencyException => new(DatabaseFailureKind.ConcurrencyConflict, "I dati sono stati modificati da un'altra postazione. Aggiornare la pagina e riprovare."),
            SqlException { Number: 2601 or 2627 } => new(DatabaseFailureKind.UniqueConstraint, "L'operazione è già stata registrata oppure utilizza un codice già esistente."),
            SqlException { Number: -2 } or TimeoutException => new(DatabaseFailureKind.Timeout, "Il database non ha risposto entro il tempo previsto. Riprovare."),
            SqlException => new(DatabaseFailureKind.Unavailable, "Database non raggiungibile. Verificare la rete e la configurazione del server."),
            _ => new(DatabaseFailureKind.Unknown, "Operazione non completata. Contattare l'amministratore del sistema.")
        };
    }

    private static Exception Unwrap(Exception exception) => exception is DbUpdateException { InnerException: not null } update
        ? update.InnerException! : exception;
}

public sealed class DatabaseHealthService(IDbContextFactory<MagazzinoDbContext> contextFactory)
{
    public async Task<DatabaseFailure?> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Database.CanConnectAsync(cancellationToken) ? null
                : new(DatabaseFailureKind.Unavailable, "Database non raggiungibile. Verificare la rete e la configurazione del server.");
        }
        catch (Exception exception) { return DatabaseErrorTranslator.Translate(exception); }
    }
}
