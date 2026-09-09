using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MagazzinoLegname.Persistence.Configurations;

public sealed class LoadConfiguration : IEntityTypeConfiguration<LoadEntity>
{
    public void Configure(EntityTypeBuilder<LoadEntity> builder)
    {
        builder.ToTable("Loads"); builder.HasKey(x => x.Id);
        builder.Property(x => x.LoadNumber).HasMaxLength(80).IsRequired(); builder.Property(x => x.Certification).HasMaxLength(40).IsRequired();
        builder.Property(x => x.DeliveryNoteNumber).HasMaxLength(80); builder.Property(x => x.ReceiptOperatorSnapshot).HasMaxLength(200);
        builder.Property(x => x.LegacyLoadNumber).HasMaxLength(100); builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SupplierId, x.LoadYear, x.AnnualProgressive }).IsUnique().HasFilter("[LoadYear] IS NOT NULL AND [AnnualProgressive] IS NOT NULL");
        builder.HasOne(x => x.Supplier).WithMany(x => x.Loads).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReceiptOperator).WithMany().HasForeignKey(x => x.ReceiptOperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LegacyImportBatch).WithMany().HasForeignKey(x => x.LegacyImportBatchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LoadNumberSequenceConfiguration : IEntityTypeConfiguration<LoadNumberSequenceEntity>
{
    public void Configure(EntityTypeBuilder<LoadNumberSequenceEntity> builder)
    {
        builder.ToTable("LoadNumberSequences"); builder.HasKey(x => new { x.SupplierId, x.LoadYear });
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MaterialGroupConfiguration : IEntityTypeConfiguration<MaterialGroupEntity>
{
    public void Configure(EntityTypeBuilder<MaterialGroupEntity> builder)
    {
        builder.ToTable("MaterialGroups"); builder.HasKey(x => x.Id); builder.Property(x => x.Quality).HasMaxLength(30).IsRequired();
        foreach (var property in new[] { nameof(MaterialGroupEntity.IncomingThickness), nameof(MaterialGroupEntity.ConventionalThickness), nameof(MaterialGroupEntity.UsefulThickness), nameof(MaterialGroupEntity.IncomingWidth), nameof(MaterialGroupEntity.WidthAfterPlaning), nameof(MaterialGroupEntity.FinalWidth), nameof(MaterialGroupEntity.IncomingLength), nameof(MaterialGroupEntity.FinalLength) })
            builder.Property(property).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.IncomingPhysicalCubicMeters).HasColumnType(SqlPrecision.Volume);
        builder.Property(x => x.LegacyEstimatedCubicMeters).HasColumnType(SqlPrecision.Volume);
        builder.Property(x => x.AppliedPrice).HasColumnType(SqlPrecision.Money); builder.Property(x => x.HistoricalValue).HasColumnType(SqlPrecision.Money);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Load).WithMany(x => x.MaterialGroups).HasForeignKey(x => x.LoadId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PackageConfiguration : IEntityTypeConfiguration<PackageEntity>
{
    public void Configure(EntityTypeBuilder<PackageEntity> builder)
    {
        builder.ToTable("Packages", table => table.HasCheckConstraint("CK_Packages_SupplementaryNoValue", "[PackageType] = 0 OR ([IncomingPhysicalCubicMeters] = 0 AND [AppliedPrice] IS NULL AND [HistoricalPackageValue] IS NULL)"));
        builder.HasKey(x => x.Id); builder.Property(x => x.PackageCode).HasMaxLength(80).IsRequired(); builder.HasIndex(x => x.PackageCode).IsUnique();
        builder.Property(x => x.Status).HasMaxLength(60).IsRequired();
        builder.Property(x => x.QrPayload).HasMaxLength(1000).IsRequired(); builder.Property(x => x.PackageType).HasConversion<int>();
        builder.Property(x => x.IncomingPhysicalCubicMeters).HasColumnType(SqlPrecision.Volume);
        builder.Property(x => x.AppliedPrice).HasColumnType(SqlPrecision.Money); builder.Property(x => x.HistoricalPackageValue).HasColumnType(SqlPrecision.Money);
        builder.Property(x => x.LegacyPackageLabel).HasMaxLength(120); builder.Property(x => x.LegacyQr).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Load).WithMany(x => x.Packages).HasForeignKey(x => x.LoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MaterialGroup).WithMany(x => x.Packages).HasForeignKey(x => x.MaterialGroupId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ClassificationMovementConfiguration : IEntityTypeConfiguration<ClassificationMovementEntity>
{
    public void Configure(EntityTypeBuilder<ClassificationMovementEntity> builder)
    {
        builder.ToTable("ClassificationMovements"); builder.HasKey(x => x.Id); builder.Property(x => x.OperatorSnapshot).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Load).WithMany().HasForeignKey(x => x.LoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MaterialGroup).WithMany(x => x.ClassificationMovements).HasForeignKey(x => x.MaterialGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WasteAdjustmentConfiguration : IEntityTypeConfiguration<WasteAdjustmentEntity>
{
    public void Configure(EntityTypeBuilder<WasteAdjustmentEntity> builder)
    {
        builder.ToTable("WasteAdjustments"); builder.HasKey(x => x.Id); builder.HasIndex(x => x.MaterialGroupId).IsUnique();
        builder.Property(x => x.OperatorSnapshot).HasMaxLength(200).IsRequired();
        foreach (var property in new[] { nameof(WasteAdjustmentEntity.AdjustmentBaseCubicMeters), nameof(WasteAdjustmentEntity.TheoreticalUsefulCubicMeters), nameof(WasteAdjustmentEntity.CubicMetersAfterWholeBoardWaste), nameof(WasteAdjustmentEntity.PartialWasteCubicMeters), nameof(WasteAdjustmentEntity.RealAvailableCubicMeters) })
            builder.Property(property).HasColumnType(SqlPrecision.Volume);
        foreach (var property in new[] { nameof(WasteAdjustmentEntity.PartialWastePercentage), nameof(WasteAdjustmentEntity.WholeBoardWastePercentage), nameof(WasteAdjustmentEntity.TotalClassificationWastePercentage) })
            builder.Property(property).HasColumnType(SqlPrecision.Percentage);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Load).WithMany().HasForeignKey(x => x.LoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MaterialGroup).WithMany(x => x.WasteAdjustments).HasForeignKey(x => x.MaterialGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupplierReturnOperationConfiguration : IEntityTypeConfiguration<SupplierReturnOperationEntity>
{
    public void Configure(EntityTypeBuilder<SupplierReturnOperationEntity> builder)
    {
        builder.ToTable("SupplierReturnOperations"); builder.HasKey(x => x.Id);
        builder.Property(x => x.OperatorSnapshot).HasMaxLength(200).IsRequired(); builder.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(1000); builder.Property(x => x.DocumentReference).HasMaxLength(120);
        builder.HasOne(x => x.Load).WithMany().HasForeignKey(x => x.LoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PackageTerminalEventConfiguration : IEntityTypeConfiguration<PackageTerminalEventEntity>
{
    public void Configure(EntityTypeBuilder<PackageTerminalEventEntity> builder)
    {
        builder.ToTable("PackageTerminalEvents"); builder.HasKey(x => x.Id); builder.HasIndex(x => x.PackageId).IsUnique();
        builder.Property(x => x.EventType).HasConversion<int>(); builder.Property(x => x.OperatorSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.InventoryCubicMeters).HasColumnType(SqlPrecision.Volume); builder.Property(x => x.ReturnedPhysicalCubicMeters).HasColumnType(SqlPrecision.Volume);
        builder.Property(x => x.Reason).HasMaxLength(200); builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasOne(x => x.Package).WithOne(x => x.TerminalEvent).HasForeignKey<PackageTerminalEventEntity>(x => x.PackageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReturnOperation).WithMany(x => x.PackageEvents).HasForeignKey(x => x.ReturnOperationId).OnDelete(DeleteBehavior.Restrict);
    }
}
