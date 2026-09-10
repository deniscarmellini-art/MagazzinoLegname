using MagazzinoLegname.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence;

public static class SqlPersistenceRoot
{
    public static IDbContextFactory<MagazzinoDbContext> ContextFactory { get; } = new MagazzinoDbContextFactory();
    public static ISupplierRepository Suppliers { get; } = new SqlSupplierRepository(ContextFactory);
    public static IOperatorRepository Operators { get; } = new SqlOperatorRepository(ContextFactory);
    public static IDomainConfigurationRepository DomainConfigurations { get; } = new SqlDomainConfigurationRepository(ContextFactory);
    public static IInboundLoadRepository InboundLoads { get; } = new SqlInboundLoadRepository(ContextFactory);
    public static IClassificationRepository Classifications { get; } = new SqlClassificationRepository(ContextFactory);
    public static IWasteAdjustmentRepository WasteAdjustments { get; } = new SqlWasteAdjustmentRepository(ContextFactory);

    public static void InitializeDatabase()
    {
        var (settings, _) = DatabaseSettingsLoader.Load();
        if (!settings.Database.Equals("MagazzinoLegname_Dev", StringComparison.OrdinalIgnoreCase))
            throw new DatabaseConfigurationException("La Fase 2A può utilizzare esclusivamente il database MagazzinoLegname_Dev.");
        using var context = ContextFactory.CreateDbContext();
        context.Database.Migrate();
    }

    public static InvalidOperationException OperatorException(Exception exception) => exception is InvalidOperationException operation
        ? operation
        : new(DatabaseErrorTranslator.Translate(exception).OperatorMessage, exception);
}
