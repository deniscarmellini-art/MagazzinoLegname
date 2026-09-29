using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MagazzinoLegname.Persistence.Configurations;
public sealed class PlannedArrivalConfiguration : IEntityTypeConfiguration<PlannedArrivalEntity>
{
 public void Configure(EntityTypeBuilder<PlannedArrivalEntity> b)
 {
  b.ToTable("PlannedArrivals", t => {
   t.HasCheckConstraint("CK_PlannedArrivals_Material", "[ConventionalThickness] IN (23,34,44) AND [Quality] IN ('C','VISTA')");
   t.HasCheckConstraint("CK_PlannedArrivals_Quantity", "[LoadQuantity] > 0 AND [ExpectedCubicMeters] > 0");
   t.HasCheckConstraint("CK_PlannedArrivals_Status", "([Status] = 0 AND [ConfirmedAt] IS NULL) OR ([Status] = 1 AND [ConfirmedAt] IS NOT NULL)");
  });
  b.HasKey(x => x.Id);
  // One aggregate cell per supplier/date in the existing calendar.
  b.HasIndex(x => new { x.SupplierId, x.Date }).IsUnique();
  b.Property(x => x.Date).HasColumnType("date");
  b.Property(x => x.SupplierNameSnapshot).HasMaxLength(300).IsRequired();
  b.Property(x => x.ConventionalThickness).HasColumnType("decimal(5,0)");
  b.Property(x => x.Quality).HasMaxLength(5).IsRequired();
  b.Property(x => x.ExpectedCubicMeters).HasColumnType("decimal(28,9)");
  b.Property(x => x.Notes).HasMaxLength(2000).IsRequired();
  b.Property(x => x.ConfirmedBy).HasMaxLength(201);
  b.Property(x => x.RowVersion).IsRowVersion();
  b.HasOne<SupplierEntity>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
 }
}
