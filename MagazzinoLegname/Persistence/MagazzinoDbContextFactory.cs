using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MagazzinoLegname.Persistence;

public sealed class MagazzinoDbContextFactory : IDbContextFactory<MagazzinoDbContext>, IDesignTimeDbContextFactory<MagazzinoDbContext>
{
    private readonly string? _baseDirectory;
    public MagazzinoDbContextFactory() { }
    public MagazzinoDbContextFactory(string baseDirectory) => _baseDirectory = baseDirectory;

    public MagazzinoDbContext CreateDbContext()
    {
        var (settings, _) = DatabaseSettingsLoader.Load(_baseDirectory);
        var options = new DbContextOptionsBuilder<MagazzinoDbContext>()
            .UseSqlServer(settings.BuildConnectionString(), sql =>
            {
                sql.CommandTimeout(Math.Clamp(settings.CommandTimeoutSeconds, 1, 600));
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            })
            .Options;
        return new MagazzinoDbContext(options);
    }

    MagazzinoDbContext IDesignTimeDbContextFactory<MagazzinoDbContext>.CreateDbContext(string[] args) => CreateDbContext();
}
