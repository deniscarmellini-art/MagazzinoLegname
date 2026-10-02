using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence;

public sealed record DatabaseStartupStatus(bool Success, string Destination, string Message, DatabaseFailureKind? FailureKind = null)
{
    public string Display => Destination + "\n" + Message;
}

public sealed class DatabaseSchemaException(string message) : Exception(message);

public static class DatabaseStartupService
{
    public const string ApplyMigrationsVariable = "MAGAZZINOLEGNAME_APPLY_MIGRATIONS";
    public static DatabaseStartupStatus CheckAndInitialize(DatabaseSettings settings, IDbContextFactory<MagazzinoDbContext> factory)
    {
        var destination = "Destinazione non validata";
        try
        {
            settings.ValidateDestination();
            destination = settings.DestinationDisplay;
            var explicitApply = System.Environment.GetEnvironmentVariable(ApplyMigrationsVariable) == "1";
            var mayApply = settings.IsDevelopment && settings.AutoMigrateDevelopment;
            if (explicitApply)
            {
                if (string.IsNullOrWhiteSpace(settings.MigrationHost) || !settings.MigrationHost.Equals(System.Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                    throw new DatabaseConfigurationException("Migration richieste da una postazione non autorizzata. Verificare MigrationHost; nessuna migration applicata.");
                mayApply = true;
            }
            using var context = factory.CreateDbContext();
            // Do not let a factory redirected by a changed configuration select another destination.
            var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(context.Database.GetConnectionString());
            if (!target.DataSource.Equals(settings.Server, StringComparison.OrdinalIgnoreCase) || !target.InitialCatalog.Equals(settings.Database, StringComparison.OrdinalIgnoreCase))
                throw new DatabaseConfigurationException("La connessione non corrisponde alla destinazione validata. Riavviare con una configurazione coerente.");
            context.Database.OpenConnection(); // Preserves SQL error codes; CanConnect would swallow them.
            var applied = context.Database.GetAppliedMigrations().ToArray();
            var migrations = context.Database.GetMigrations().ToArray();
            if (applied.Except(migrations).Any()) throw new DatabaseSchemaException("Il database contiene migration non riconosciute da questa versione. Aggiornare l'applicazione; nessuna modifica applicata.");
            var pending = migrations.Except(applied).ToArray();
            if (pending.Length == 0)
                return new(true, destination, "Connessione riuscita. Schema aggiornato; nessuna migration eseguita.");
            if (!mayApply)
                throw new DatabaseSchemaException($"Schema non aggiornato: {pending.Length} migration pendenti. Avvio bloccato senza modificare lo schema. Richiedere l'aggiornamento dalla postazione autorizzata con {ApplyMigrationsVariable}=1.");
            // Session lock serializes authorized maintenance starts without altering schema or services.
            var locked = false;
            try
            {
                context.Database.ExecuteSqlRaw("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'MagazzinoLegname.SchemaUpgrade', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; IF @r<0 THROW 51001, 'Aggiornamento schema gia in corso.', 1;");
                locked = true;
                if (context.Database.GetPendingMigrations().Any()) context.Database.Migrate();
                if (context.Database.GetPendingMigrations().Any()) throw new DatabaseSchemaException("Aggiornamento schema non completato. Avvio bloccato.");
            }
            finally
            {
                if (locked)
                {
                    try { context.Database.ExecuteSqlRaw("EXEC sys.sp_releaseapplock @Resource=N'MagazzinoLegname.SchemaUpgrade', @LockOwner='Session';"); }
                    catch (Exception exception) { PersistenceDebugLog.WriteException("Release schema maintenance lock", exception); Microsoft.Data.SqlClient.SqlConnection.ClearPool((Microsoft.Data.SqlClient.SqlConnection)context.Database.GetDbConnection()); }
                }
            }
            return new(true, destination, "Connessione riuscita. Migration applicate dalla postazione autorizzata; schema aggiornato.");
        }
        catch (Exception exception)
        {
            PersistenceDebugLog.WriteException("Database startup", exception);
            var failure = DatabaseErrorTranslator.Translate(exception);
            return new(false, destination, failure.OperatorMessage, failure.Kind);
        }
    }
}
