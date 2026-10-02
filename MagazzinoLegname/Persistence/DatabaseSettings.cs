using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace MagazzinoLegname.Persistence;

public sealed class DatabaseSettings
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public string Environment { get; set; } = "Shared";
    public string[] AllowedServers { get; set; } = [];
    public string[] AllowedDatabases { get; set; } = [];
    public bool AutoMigrateDevelopment { get; set; } = true;
    public string? MigrationHost { get; set; }
    public string Authentication { get; set; } = "Windows";
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public int CommandTimeoutSeconds { get; set; } = 30;

    public bool IsDevelopment => string.Equals(Environment, "Development", StringComparison.OrdinalIgnoreCase);
    public string DestinationDisplay => $"Ambiente: {Environment} · Server: {Server} · Database: {Database}";
    public void ValidateDestination()
    {
        if (!IsDevelopment && !string.Equals(Environment, "Shared", StringComparison.OrdinalIgnoreCase))
            throw new DatabaseConfigurationException("Environment deve essere Development oppure Shared.");
        // Reject connection-string fragments and control characters before displaying destination fields.
        if (string.IsNullOrWhiteSpace(Server) || !Regex.IsMatch(Server, @"^[\p{L}\p{N}_.\\,:\[\]-]+$")
            || string.IsNullOrWhiteSpace(Database) || !Regex.IsMatch(Database, @"^[\p{L}\p{N}_. -]+$"))
            throw new DatabaseConfigurationException("Server o Database mancante/non valido. Specificare nomi, non connection string complete.");
        if (AllowedServers is null || !AllowedServers.Contains(Server, StringComparer.OrdinalIgnoreCase)
            || AllowedDatabases is null || !AllowedDatabases.Contains(Database, StringComparer.OrdinalIgnoreCase))
            throw new DatabaseConfigurationException("Destinazione non autorizzata: verificare Environment, AllowedServers e AllowedDatabases nel file di configurazione.");
        if (!string.Equals(Authentication, "Windows", StringComparison.OrdinalIgnoreCase))
            throw new DatabaseConfigurationException("È supportata esclusivamente Windows Authentication. Non configurare credenziali SQL.");
    }
    public string BuildConnectionString()
    {
        ValidateDestination();
        return new SqlConnectionStringBuilder {
            DataSource = Server, InitialCatalog = Database, IntegratedSecurity = true,
            Encrypt = Encrypt, TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = Math.Clamp(ConnectionTimeoutSeconds, 1, 120),
            MultipleActiveResultSets = true, ApplicationName = "MagazzinoLegname"
        }.ConnectionString;
    }
}

public sealed class DatabaseSettingsDocument
{
    public DatabaseSettings Database { get; set; } = new();
}

public static class DatabaseSettingsLoader
{
    public const string SettingsPathEnvironmentVariable = "MAGAZZINOLEGNAME_DB_SETTINGS";
    public static (DatabaseSettings Settings, string Path) Load(string? baseDirectory = null)
    {
        var configuredPath = System.Environment.GetEnvironmentVariable(SettingsPathEnvironmentVariable);
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "database.settings.json") : Path.GetFullPath(configuredPath);
        if (!File.Exists(path)) throw new DatabaseConfigurationException("File database.settings.json non trovato. Verificare la configurazione esterna.");
        try
        {
            var json = File.ReadAllText(path);
            using var parsed = JsonDocument.Parse(json);
            // Do not silently accept old SQL credentials, including alternate casing.
            foreach (var section in parsed.RootElement.EnumerateObject().Where(x => x.Name.Equals("Database", StringComparison.OrdinalIgnoreCase)))
                foreach (var property in section.Value.EnumerateObject())
                    if (property.Name.Equals("Password", StringComparison.OrdinalIgnoreCase) || property.Name.Equals("UserId", StringComparison.OrdinalIgnoreCase))
                        throw new DatabaseConfigurationException("Rimuovere UserId/Password dalla configurazione: viene usata l'identità Windows.");
            var document = JsonSerializer.Deserialize<DatabaseSettingsDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new DatabaseConfigurationException("Configurazione database vuota o non valida.");
            if (document.Database is null) throw new DatabaseConfigurationException("Sezione Database mancante.");
            document.Database.ValidateDestination();
            return (document.Database, path);
        }
        catch (JsonException exception) { throw new DatabaseConfigurationException("Il file database.settings.json non è valido.", exception); }
        catch (InvalidOperationException exception) { throw new DatabaseConfigurationException("La struttura del file database.settings.json non è valida.", exception); }
    }
}

public sealed class DatabaseConfigurationException(string message, Exception? innerException = null) : Exception(message, innerException);
