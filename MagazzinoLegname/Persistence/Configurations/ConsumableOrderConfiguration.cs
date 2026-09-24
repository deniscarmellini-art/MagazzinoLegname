using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MagazzinoLegname.Persistence.Configurations;

public sealed class ConsumableOrderConfiguration : IEntityTypeConfiguration<ConsumableOrderEntity>
{
    public void Configure(EntityTypeBuilder<ConsumableOrderEntity> builder)
    {
        builder.ToTable("ConsumableOrders", table =>
        {
            table.HasCheckConstraint("CK_ConsumableOrders_Quantity", "[OrderedQuantity] > 0");
            table.HasCheckConstraint("CK_ConsumableOrders_Status", "([Status] IN (1,2) AND [ClosedAtUtc] IS NULL) OR ([Status] IN (3,4) AND [ClosedAtUtc] IS NOT NULL)");
            table.HasCheckConstraint("CK_ConsumableOrders_Delivery", "[ExpectedDeliveryDate] IS NULL OR [ExpectedDeliveryDate] >= [OrderedAt]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderedAt).HasColumnType("date");
        builder.Property(x => x.ExpectedDeliveryDate).HasColumnType("date");
        builder.Property(x => x.OrderedQuantity).HasColumnType("decimal(19,6)");
        builder.Property(x => x.SupplierNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ProductNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.UnitOfMeasureSnapshot).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne<ConsumableItemEntity>().WithMany().HasForeignKey(x => x.ConsumableItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ConsumableItemId, x.Status });
    }
}
