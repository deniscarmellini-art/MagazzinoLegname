using MagazzinoLegname.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence;

public static class SqlPersistenceRoot
{
    public static IDbContextFactory<MagazzinoDbContext> ContextFactory { get; } = new MagazzinoDbContextFactory();
    public static IConsumableOrderRepository ConsumableOrders { get; } = new SqlConsumableOrderRepository(ContextFactory);
    public static IConsumableInventoryRepository ConsumableInventories { get; } = new SqlConsumableInventoryRepository(ContextFactory);
    public static IConsumableRepository Consumables { get; } = new SqlConsumableRepository(ContextFactory);
    public static ISupplierRepository Suppliers { get; } = new SqlSupplierRepository(ContextFactory);
    public static IOperatorRepository Operators { get; } = new SqlOperatorRepository(ContextFactory);
    public static IDomainConfigurationRepository DomainConfigurations { get; } = new SqlDomainConfigurationRepository(ContextFactory);
    public static IInboundLoadRepository InboundLoads { get; } = new SqlInboundLoadRepository(ContextFactory);
    public static IClassificationRepository Classifications { get; } = new SqlClassificationRepository(ContextFactory);
    public static IWasteAdjustmentRepository WasteAdjustments { get; } = new SqlWasteAdjustmentRepository(ContextFactory);
    public static IPackageTerminalRepository PackageTerminals { get; } = new SqlPackageTerminalRepository(ContextFactory);

    public static DatabaseStartupStatus? StartupStatus { get; private set; }
    public static DatabaseStartupStatus InitializeDatabase()
    {
        try
        {
            var settings = ((MagazzinoDbContextFactory)ContextFactory).Settings;
            return StartupStatus = DatabaseStartupService.CheckAndInitialize(settings, ContextFactory);
        }
        catch (Exception exception)
        {
            PersistenceDebugLog.WriteException("Load database configuration", exception);
            var failure = DatabaseErrorTranslator.Translate(exception);
            return StartupStatus = new(false, "Configurazione database non validata", failure.OperatorMessage, failure.Kind);
        }
    }

    public static InvalidOperationException OperatorException(Exception exception) => exception is InvalidOperationException operation
        ? operation
        : new(DatabaseErrorTranslator.Translate(exception).OperatorMessage, exception);
}
