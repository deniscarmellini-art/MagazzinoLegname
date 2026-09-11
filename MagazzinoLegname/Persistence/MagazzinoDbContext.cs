using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MagazzinoLegname.Persistence;

public sealed class MagazzinoDbContext(DbContextOptions<MagazzinoDbContext> options) : DbContext(options)
{
    public DbSet<ConsumableItemEntity> ConsumableItems => Set<ConsumableItemEntity>();
    public DbSet<SupplierEntity> Suppliers => Set<SupplierEntity>();
    public DbSet<SupplierContactEntity> SupplierContacts => Set<SupplierContactEntity>();
    public DbSet<SupplierThicknessConfigurationEntity> SupplierThicknessConfigurations => Set<SupplierThicknessConfigurationEntity>();
    public DbSet<SupplierPriceEntity> SupplierPrices => Set<SupplierPriceEntity>();
    public DbSet<OperatorEntity> Operators => Set<OperatorEntity>();
    public DbSet<ThicknessFamilyEntity> ThicknessFamilies => Set<ThicknessFamilyEntity>();
    public DbSet<ApplicationSettingsEntity> ApplicationSettings => Set<ApplicationSettingsEntity>();
    public DbSet<LoadEntity> Loads => Set<LoadEntity>();
    public DbSet<LoadNumberSequenceEntity> LoadNumberSequences => Set<LoadNumberSequenceEntity>();
    public DbSet<MaterialGroupEntity> MaterialGroups => Set<MaterialGroupEntity>();
    public DbSet<PackageEntity> Packages => Set<PackageEntity>();
    public DbSet<ClassificationMovementEntity> ClassificationMovements => Set<ClassificationMovementEntity>();
    public DbSet<WasteAdjustmentEntity> WasteAdjustments => Set<WasteAdjustmentEntity>();
    public DbSet<SupplierReturnOperationEntity> SupplierReturnOperations => Set<SupplierReturnOperationEntity>();
    public DbSet<PackageTerminalEventEntity> PackageTerminalEvents => Set<PackageTerminalEventEntity>();
    public DbSet<LegacyImportBatchEntity> LegacyImportBatches => Set<LegacyImportBatchEntity>();
    public DbSet<LegacyImportKeyEntity> LegacyImportKeys => Set<LegacyImportKeyEntity>();
    public DbSet<LegacyHistoricalRecordEntity> LegacyHistoricalRecords => Set<LegacyHistoricalRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MagazzinoDbContext).Assembly);
    }
}
