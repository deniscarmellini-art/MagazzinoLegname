using System.Data.Common;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using MagazzinoLegname.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL " + message);
        Console.WriteLine("PASS " + message);
    }
    private static DatabaseSettings Copy(DatabaseSettings settings) => JsonSerializer.Deserialize<DatabaseSettings>(JsonSerializer.Serialize(settings))!;
    private static int Main()
    {
        var oldApply = Environment.GetEnvironmentVariable(DatabaseStartupService.ApplyMigrationsVariable);
        Environment.SetEnvironmentVariable(DatabaseStartupService.ApplyMigrationsVariable, null);
        string? createdName = null;
        string? masterConnection = null;
        try
        {
            var (dev, _) = DatabaseSettingsLoader.Load();
            Check(dev.IsDevelopment, "A configurazione Development esplicita");
            var devFactory = new TestFactory(dev, readOnly: true);
            using (var db = devFactory.CreateDbContext())
                Check(!db.Database.GetPendingMigrations().Any(), "Dev già aggiornato: nessuna modifica reale autorizzata");
            var status = DatabaseStartupService.CheckAndInitialize(dev, devFactory);
            Check(status.Success && devFactory.Audit.Writes == 0, "A avvio database DEV corretto, nessuna scrittura");
            Console.WriteLine(status.Display);
            var shared = Copy(dev); shared.Environment = "Shared"; shared.Database = "MagazzinoLegname";
            shared.AllowedDatabases = [shared.Database]; shared.AutoMigrateDevelopment = false;
            Check(new SqlConnectionStringBuilder(shared.BuildConnectionString()).InitialCatalog == "MagazzinoLegname", "B nome database condiviso accettato (solo configurazione, nessuna creazione)");
            var wrong = Copy(shared); wrong.Database = "AltroDatabase";
            var neverFactory = new TestFactory(wrong);
            status = DatabaseStartupService.CheckAndInitialize(wrong, neverFactory);
            Check(!status.Success && status.FailureKind == DatabaseFailureKind.Configuration && neverFactory.Contexts == 0, "Database fuori allowlist respinto prima della connessione");
            wrong = Copy(shared); wrong.Server = "altro-server";
            neverFactory = new TestFactory(wrong);
            Check(!DatabaseStartupService.CheckAndInitialize(wrong, neverFactory).Success && neverFactory.Contexts == 0, "Server fuori allowlist respinto prima della connessione");
            wrong = Copy(shared); wrong.Authentication = "SqlServer";
            Check(!DatabaseStartupService.CheckAndInitialize(wrong, new TestFactory(wrong)).Success, "SQL Authentication respinta");

            var testName = "MagazzinoLegname_TestStartup_" + Guid.NewGuid().ToString("N");
            var test = Copy(shared); test.Database = testName; test.AllowedDatabases = [testName]; test.MigrationHost = Environment.MachineName;
            var master = new SqlConnectionStringBuilder(dev.BuildConnectionString()) { InitialCatalog = "master" };
            masterConnection = master.ConnectionString;
            using (var connection = new SqlConnection(masterConnection))
            {
                connection.Open(); using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{testName}]"; command.ExecuteNonQuery(); createdName = testName;
            }
            var factory = new TestFactory(test);
            status = DatabaseStartupService.CheckAndInitialize(test, factory);
            Check(!status.Success && status.FailureKind == DatabaseFailureKind.SchemaOutdated && factory.Audit.Writes == 0, "C Shared senza flag: schema pendente bloccato senza DDL");
            Console.WriteLine(status.Display);
            using (var db = factory.CreateDbContext()) Check(UserTableCount(db) == 0, "C database test ancora vuoto");
            var health = new DatabaseHealthService(factory).CheckAsync().GetAwaiter().GetResult();
            Check(health?.Kind == DatabaseFailureKind.SchemaOutdated && factory.Audit.Writes == 0, "Diagnostica schema read-only");
            Environment.SetEnvironmentVariable(DatabaseStartupService.ApplyMigrationsVariable, "1");
            var forbidden = Copy(test); forbidden.MigrationHost = "POSTAZIONE-NON-AUTORIZZATA";
            neverFactory = new TestFactory(forbidden);
            status = DatabaseStartupService.CheckAndInitialize(forbidden, neverFactory);
            Check(!status.Success && neverFactory.Contexts == 0, "Flag presente ma postazione non autorizzata: bloccato");
            using (var competing = new SqlConnection(test.BuildConnectionString()))
            {
                competing.Open(); using var command = competing.CreateCommand();
                command.CommandText = "EXEC sys.sp_getapplock @Resource=N'MagazzinoLegname.SchemaUpgrade', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0;";
                command.ExecuteNonQuery();
                status = DatabaseStartupService.CheckAndInitialize(test, factory);
                Check(!status.Success && status.FailureKind == DatabaseFailureKind.MaintenanceBusy, "Migration concorrente respinta dal lock SQL");
                command.CommandText = "EXEC sys.sp_releaseapplock @Resource=N'MagazzinoLegname.SchemaUpgrade', @LockOwner='Session';"; command.ExecuteNonQuery();
            }
            status = DatabaseStartupService.CheckAndInitialize(test, factory);
            Check(status.Success, "D migration autorizzate sul database test: " + status.Message);
            int migrations;
            using (var db = factory.CreateDbContext())
            {
                migrations = db.Database.GetAppliedMigrations().Count();
                Check(migrations == db.Database.GetMigrations().Count() && !db.Database.GetPendingMigrations().Any(), "D tutte le migration applicate una volta");
            }
            Environment.SetEnvironmentVariable(DatabaseStartupService.ApplyMigrationsVariable, null);
            var readFactory = new TestFactory(test, readOnly: true);
            status = DatabaseStartupService.CheckAndInitialize(test, readFactory);
            Check(status.Success && readFactory.Audit.Writes == 0, "E secondo avvio Shared: nessuna migration/DDL tentata");
            using (var db = readFactory.CreateDbContext()) Check(db.Database.GetAppliedMigrations().Count() == migrations, "E cronologia migration invariata");
            Check(new DatabaseHealthService(readFactory).CheckAsync().GetAwaiter().GetResult() == null, "Diagnostica connessione riuscita e schema aggiornato");
            var unreachable = Copy(test); unreachable.Server = "tcp:127.0.0.1,1"; unreachable.AllowedServers = [unreachable.Server]; unreachable.ConnectionTimeoutSeconds = 1;
            status = DatabaseStartupService.CheckAndInitialize(unreachable, new TestFactory(unreachable));
            Check(!status.Success && status.FailureKind is DatabaseFailureKind.Unavailable or DatabaseFailureKind.Timeout, "F server non raggiungibile gestito senza eccezione non intercettata");
            Check(!status.Display.Contains("StackTrace") && !status.Display.Contains("SqlException"), "F messaggio operatore senza stack trace tecnico");
            var absent = Copy(test); absent.Database = "MagazzinoLegname_TestAbsent_" + Guid.NewGuid().ToString("N"); absent.AllowedDatabases = [absent.Database];
            status = DatabaseStartupService.CheckAndInitialize(absent, new TestFactory(absent));
            Check(!status.Success && status.FailureKind == DatabaseFailureKind.DatabaseUnavailable, "Database inesistente distinto da server irraggiungibile");
            Check(DatabaseErrorTranslator.FromSqlNumber(18456).Kind == DatabaseFailureKind.WindowsLoginDenied, "Diagnostica login Windows non autorizzato");
            Check(DatabaseErrorTranslator.FromSqlNumber(-2).Kind == DatabaseFailureKind.Timeout, "Diagnostica timeout");
            Console.WriteLine("ALL A-F PASS; nessun database produttivo creato o modificato.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally
        {
            Environment.SetEnvironmentVariable(DatabaseStartupService.ApplyMigrationsVariable, oldApply);
            if (createdName is not null && masterConnection is not null)
            {
                if (!Regex.IsMatch(createdName, "^MagazzinoLegname_TestStartup_[a-f0-9]{32}$")) throw new Exception("Cleanup guard");
                SqlConnection.ClearAllPools();
                using var connection = new SqlConnection(masterConnection); connection.Open();
                using var command = connection.CreateCommand(); command.CommandText = $"ALTER DATABASE [{createdName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{createdName}]"; command.ExecuteNonQuery();
                Console.WriteLine("Database temporaneo rimosso: " + createdName);
            }
        }
    }
    private static int UserTableCount(MagazzinoDbContext db)
    {
        db.Database.OpenConnection(); using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0"; return Convert.ToInt32(command.ExecuteScalar());
    }
    private sealed class TestFactory(DatabaseSettings settings, bool readOnly = false) : IDbContextFactory<MagazzinoDbContext>
    {
        public AuditInterceptor Audit { get; } = new(readOnly);
        public int Contexts { get; private set; }
        public MagazzinoDbContext CreateDbContext()
        {
            Contexts++;
            return new(new DbContextOptionsBuilder<MagazzinoDbContext>().UseSqlServer(settings.BuildConnectionString(), sql=>sql.CommandTimeout(30)).AddInterceptors(Audit).Options);
        }
    }
    private sealed class AuditInterceptor(bool readOnly) : DbCommandInterceptor
    {
        public int Writes { get; private set; }
        private void Inspect(DbCommand command)
        {
            if (!Regex.IsMatch(command.CommandText, @"\b(CREATE|ALTER|DROP|INSERT|UPDATE|DELETE|MERGE|EXEC)\b", RegexOptions.IgnoreCase)) return;
            Writes++;
            if (readOnly) throw new InvalidOperationException("Il test proibisce qualunque DDL/DML o EXEC su questa destinazione.");
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result) { Inspect(command); return result; }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result) { Inspect(command); return result; }
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result) { Inspect(command); return result; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct=default) { Inspect(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct=default) { Inspect(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken ct=default) { Inspect(command); return ValueTask.FromResult(result); }
    }
}
