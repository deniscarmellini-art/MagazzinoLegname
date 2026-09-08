using System.Text.Json;
using System.IO;

namespace MagazzinoLegname.Persistence;

public sealed class DatabaseSettings
{
    public string Server { get; set; } = @".\SQLEXPRESS";
    public string Database { get; set; } = "MagazzinoLegname";
    public string Authentication { get; set; } = "Windows";
    public string? UserId { get; set; }
    public string? Password { get; set; }
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public int CommandTimeoutSeconds { get; set; } = 30;

    public string BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Database))
            throw new DatabaseConfigurationException("Configurare Server e Database in database.settings.json.");
        var sqlAuthentication = Authentication.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
        if (sqlAuthentication && (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Password)))
            throw new DatabaseConfigurationException("Per l'autenticazione SQL Server sono obbligatori UserId e Password.");
        static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
        var authentication = sqlAuthentication
            ? $"User ID={Quote(UserId!.Trim())};Password={Quote(Password!)};"
            : "Integrated Security=True;";
        return $"Server={Quote(Server.Trim())};Database={Quote(Database.Trim())};{authentication}" +
               $"Encrypt={Encrypt};TrustServerCertificate={TrustServerCertificate};" +
               $"Connect Timeout={Math.Clamp(ConnectionTimeoutSeconds, 1, 120)};" +
               "MultipleActiveResultSets=True;Application Name=MagazzinoLegname";
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
        var configuredPath = Environment.GetEnvironmentVariable(SettingsPathEnvironmentVariable);
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "database.settings.json")
            : Path.GetFullPath(configuredPath);
        if (!File.Exists(path)) throw new DatabaseConfigurationException($"Configurazione database non trovata: {path}");
        try
        {
            var document = JsonSerializer.Deserialize<DatabaseSettingsDocument>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new DatabaseConfigurationException("Configurazione database vuota o non valida.");
            return (document.Database, path);
        }
        catch (JsonException exception)
        {
            throw new DatabaseConfigurationException("Il file database.settings.json non è valido.", exception);
        }
    }
}

public sealed class DatabaseConfigurationException(string message, Exception? innerException = null) : Exception(message, innerException);
