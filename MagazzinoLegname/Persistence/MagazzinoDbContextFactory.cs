using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
#if DEBUG
using Microsoft.Extensions.Logging;
#endif

namespace MagazzinoLegname.Persistence;

public sealed class MagazzinoDbContextFactory : IDbContextFactory<MagazzinoDbContext>, IDesignTimeDbContextFactory<MagazzinoDbContext>
{
    private readonly Lazy<DatabaseSettings> _settings;
    public MagazzinoDbContextFactory() : this(null) { }
    public MagazzinoDbContextFactory(string? baseDirectory) => _settings = new(() => DatabaseSettingsLoader.Load(baseDirectory).Settings);
    internal DatabaseSettings Settings => _settings.Value;

    public MagazzinoDbContext CreateDbContext()
    {
        var settings = Settings;
        var optionsBuilder = new DbContextOptionsBuilder<MagazzinoDbContext>()
            .UseSqlServer(settings.BuildConnectionString(), sql =>
            {
                sql.CommandTimeout(Math.Clamp(settings.CommandTimeoutSeconds, 1, 600));
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });
#if DEBUG
        if (settings.IsDevelopment)
        {
            optionsBuilder.EnableDetailedErrors()
                .LogTo(PersistenceDebugLog.WriteEfCommand,
                    [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
        }
#endif
        return new MagazzinoDbContext(optionsBuilder.Options);
    }

    MagazzinoDbContext IDesignTimeDbContextFactory<MagazzinoDbContext>.CreateDbContext(string[] args) => CreateDbContext();
}
