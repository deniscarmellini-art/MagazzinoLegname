using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MagazzinoLegname.Persistence.Configurations;

public sealed class ConsumableItemConfiguration : IEntityTypeConfiguration<ConsumableItemEntity>
{
    public void Configure(EntityTypeBuilder<ConsumableItemEntity> builder)
    {
        builder.ToTable("ConsumableItems", table =>
        {
            table.HasCheckConstraint("CK_ConsumableItems_QuantityPerUnit", "[QuantityPerUnit] IS NULL OR [QuantityPerUnit] > 0");
            table.HasCheckConstraint("CK_ConsumableItems_BaseParameters", "([MinimumStock] IS NULL OR [MinimumStock] >= 0) AND ([ConsumptionAverageQuantity] IS NULL OR [ConsumptionAverageQuantity] >= 0) AND ([LeadTimeDays] IS NULL OR [LeadTimeDays] >= 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InternalCode).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.InternalCode).IsUnique();
        builder.Property(x => x.ProductName).HasMaxLength(300).IsRequired();
        // Consumable suppliers remain free text, independent of timber Suppliers.
        builder.Property(x => x.SupplierName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Department).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(40).IsRequired();
        builder.Property(x => x.QuantityPerUnit).HasColumnType(SqlPrecision.Quantity);
        builder.Property(x => x.MinimumStock).HasColumnType(SqlPrecision.Quantity);
        builder.Property(x => x.ConsumptionAverageQuantity).HasColumnType(SqlPrecision.Quantity);
        builder.Property(x => x.ConsumptionAverageText).HasMaxLength(500).IsRequired();
        builder.Property(x => x.LeadTimeText).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Packaging).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Notes).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
