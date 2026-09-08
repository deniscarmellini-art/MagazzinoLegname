using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MagazzinoLegname.Persistence.Configurations;

public sealed class LegacyImportBatchConfiguration : IEntityTypeConfiguration<LegacyImportBatchEntity>
{
    public void Configure(EntityTypeBuilder<LegacyImportBatchEntity> builder)
    {
        builder.ToTable("LegacyImportBatches"); builder.HasKey(x => x.Id); builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired(); builder.Property(x => x.FileFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.ImportedBySnapshot).HasMaxLength(200); builder.HasIndex(x => new { x.Kind, x.FileFingerprint }).IsUnique();
        builder.Property(x => x.PhysicalCubicMeters).HasColumnType(SqlPrecision.Volume); builder.Property(x => x.LegacyAvailableCubicMeters).HasColumnType(SqlPrecision.Volume);
        builder.HasOne(x => x.ImportedByOperator).WithMany().HasForeignKey(x => x.ImportedByOperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LegacyImportKeyConfiguration : IEntityTypeConfiguration<LegacyImportKeyEntity>
{
    public void Configure(EntityTypeBuilder<LegacyImportKeyEntity> builder)
    {
        builder.ToTable("LegacyImportKeys"); builder.HasKey(x => x.Id); builder.Property(x => x.ImportKey).HasMaxLength(500).IsRequired(); builder.HasIndex(x => x.ImportKey).IsUnique();
        builder.HasOne(x => x.Batch).WithMany(x => x.ImportKeys).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LegacyHistoricalRecordConfiguration : IEntityTypeConfiguration<LegacyHistoricalRecordEntity>
{
    public void Configure(EntityTypeBuilder<LegacyHistoricalRecordEntity> builder)
    {
        builder.ToTable("LegacyHistoricalRecords"); builder.HasKey(x => x.Id);
        builder.Property(x => x.ImportKey).HasMaxLength(500).IsRequired(); builder.HasIndex(x => x.ImportKey).IsUnique();
        builder.Property(x => x.SupplierNameSnapshot).HasMaxLength(200).IsRequired(); builder.Property(x => x.LoadNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PackageLabel).HasMaxLength(120); builder.Property(x => x.QualityOriginal).HasMaxLength(50); builder.Property(x => x.QualityNormalized).HasMaxLength(50); builder.Property(x => x.Certification).HasMaxLength(50);
        builder.Property(x => x.Pieces).HasColumnType(SqlPrecision.Quantity);
        builder.Property(x => x.IncomingThickness).HasColumnType(SqlPrecision.Dimension); builder.Property(x => x.IncomingWidth).HasColumnType(SqlPrecision.Dimension); builder.Property(x => x.IncomingLength).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.PhysicalCubicMeters).HasColumnType(SqlPrecision.Volume); builder.Property(x => x.LegacyAvailableCubicMeters).HasColumnType(SqlPrecision.Volume);
        builder.HasOne(x => x.Batch).WithMany(x => x.HistoricalRecords).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
    }
}
