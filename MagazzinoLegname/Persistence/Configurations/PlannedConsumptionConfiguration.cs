using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MagazzinoLegname.Persistence.Configurations;
public sealed class PlannedConsumptionConfiguration : IEntityTypeConfiguration<PlannedConsumptionEntity>
{
    public void Configure(EntityTypeBuilder<PlannedConsumptionEntity> b)
    {
        b.ToTable("PlannedConsumptions", t => {
            t.HasCheckConstraint("CK_PlannedConsumptions_WeekStart", "DATEDIFF(day, '19000101', [WeekStart]) % 7 = 0");
            t.HasCheckConstraint("CK_PlannedConsumptions_Material", "[ConventionalThickness] IN (23,34,44) AND [Quality] IN ('C','VISTA')");
            t.HasCheckConstraint("CK_PlannedConsumptions_Quantity", "[ExpectedCubicMeters] > 0");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.WeekStart, x.ConventionalThickness, x.Quality }).IsUnique();
        b.Property(x => x.WeekStart).HasColumnType("date");
        b.Property(x => x.ConventionalThickness).HasColumnType("decimal(5,0)");
        b.Property(x => x.Quality).HasMaxLength(5).IsRequired();
        b.Property(x => x.ExpectedCubicMeters).HasColumnType("decimal(28,9)");
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
