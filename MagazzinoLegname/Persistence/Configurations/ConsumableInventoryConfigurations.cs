using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MagazzinoLegname.Persistence.Configurations;

public sealed class ConsumableInventorySessionConfiguration : IEntityTypeConfiguration<ConsumableInventorySessionEntity>
{
    public void Configure(EntityTypeBuilder<ConsumableInventorySessionEntity> builder)
    {
        builder.ToTable("ConsumableInventorySessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InventoryDate).HasColumnType("date");
        builder.Property(x => x.OperatorSnapshot).HasMaxLength(201).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne<OperatorEntity>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InventoryDate, x.CreatedAtUtc });
    }
}

public sealed class ConsumableInventoryReadingConfiguration : IEntityTypeConfiguration<ConsumableInventoryReadingEntity>
{
    public void Configure(EntityTypeBuilder<ConsumableInventoryReadingEntity> builder)
    {
        builder.ToTable("ConsumableInventoryReadings", table =>
        {
            table.HasCheckConstraint("CK_ConsumableInventoryReadings_Quantities", "[CountedUnits] >= 0 AND [QuantityPerUnitSnapshot] > 0 AND [CalculatedQuantity] >= 0");
            table.HasCheckConstraint("CK_ConsumableInventoryReadings_Calculation", "[CalculatedQuantity] = [CountedUnits] * [QuantityPerUnitSnapshot]");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SessionId, x.ConsumableItemId }).IsUnique();
        builder.HasIndex(x => x.ConsumableItemId);
        // 18+19+1 = 38: multiplication keeps all 12 fractional digits in SQL Server.
        builder.Property(x => x.CountedUnits).HasColumnType("decimal(18,6)");
        builder.Property(x => x.QuantityPerUnitSnapshot).HasColumnType("decimal(19,6)");
        builder.Property(x => x.CalculatedQuantity).HasColumnType("decimal(28,12)");
        builder.Property(x => x.ProductSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.SupplierSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.DepartmentSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnitOfMeasureSnapshot).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Session).WithMany(x => x.Readings).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConsumableItemEntity>().WithMany().HasForeignKey(x => x.ConsumableItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
