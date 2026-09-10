using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
#if DEBUG
using Microsoft.Extensions.Logging;
#endif

namespace MagazzinoLegname.Persistence;

public sealed class MagazzinoDbContextFactory : IDbContextFactory<MagazzinoDbContext>, IDesignTimeDbContextFactory<MagazzinoDbContext>
{
    private readonly string? _baseDirectory;
    public MagazzinoDbContextFactory() { }
    public MagazzinoDbContextFactory(string baseDirectory) => _baseDirectory = baseDirectory;

    public MagazzinoDbContext CreateDbContext()
    {
        var (settings, _) = DatabaseSettingsLoader.Load(_baseDirectory);
        var optionsBuilder = new DbContextOptionsBuilder<MagazzinoDbContext>()
            .UseSqlServer(settings.BuildConnectionString(), sql =>
            {
                sql.CommandTimeout(Math.Clamp(settings.CommandTimeoutSeconds, 1, 600));
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            });
#if DEBUG
        if (settings.Database.Equals("MagazzinoLegname_Dev", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .LogTo(PersistenceDebugLog.WriteEfCommand,
                    [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
        }
#endif
        return new MagazzinoDbContext(optionsBuilder.Options);
    }

    MagazzinoDbContext IDesignTimeDbContextFactory<MagazzinoDbContext>.CreateDbContext(string[] args) => CreateDbContext();
}
