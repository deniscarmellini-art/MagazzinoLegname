using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MagazzinoLegname.Persistence.Configurations;

internal static class SqlPrecision
{
    public const string Dimension = "decimal(12,4)";
    public const string Volume = "decimal(19,9)";
    public const string Money = "decimal(19,4)";
    public const string Percentage = "decimal(9,4)";
    public const string Quantity = "decimal(19,6)";
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<SupplierEntity>
{
    public void Configure(EntityTypeBuilder<SupplierEntity> builder)
    {
        builder.ToTable("Suppliers"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(16).IsRequired(); builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.VatNumber).HasMaxLength(32); builder.Property(x => x.TaxCode).HasMaxLength(32); builder.Property(x => x.Address).HasMaxLength(250);
        builder.Property(x => x.PostalCode).HasMaxLength(16); builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Province).HasMaxLength(8); builder.Property(x => x.Country).HasMaxLength(100); builder.Property(x => x.Email).HasMaxLength(254); builder.Property(x => x.CertifiedEmail).HasMaxLength(254);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class SupplierContactConfiguration : IEntityTypeConfiguration<SupplierContactEntity>
{
    public void Configure(EntityTypeBuilder<SupplierContactEntity> builder)
    {
        builder.ToTable("SupplierContacts"); builder.HasKey(x => x.Id);
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired(); builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(100); builder.Property(x => x.Phone).HasMaxLength(40);
        builder.Property(x => x.Mobile).HasMaxLength(40); builder.Property(x => x.Email).HasMaxLength(254);
        builder.HasOne(x => x.Supplier).WithMany(x => x.Contacts).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SupplierThicknessConfigurationConfiguration : IEntityTypeConfiguration<SupplierThicknessConfigurationEntity>
{
    public void Configure(EntityTypeBuilder<SupplierThicknessConfigurationEntity> builder)
    {
        builder.ToTable("SupplierThicknessConfigurations"); builder.HasKey(x => x.Id);
        builder.Property(x => x.ConventionalThickness).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.PlaningReductionMillimeters).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.SupplierId, x.ConventionalThickness }).IsUnique();
        builder.HasOne(x => x.Supplier).WithMany(x => x.ThicknessConfigurations).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SupplierPriceConfiguration : IEntityTypeConfiguration<SupplierPriceEntity>
{
    public void Configure(EntityTypeBuilder<SupplierPriceEntity> builder)
    {
        builder.ToTable("SupplierPrices"); builder.HasKey(x => x.Id);
        builder.Property(x => x.ConventionalThickness).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.PricePerCubicMeter).HasColumnType(SqlPrecision.Money);
        builder.HasIndex(x => new { x.SupplierId, x.ConventionalThickness, x.ValidFrom }).IsUnique();
        builder.HasOne(x => x.Supplier).WithMany(x => x.Prices).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class OperatorConfiguration : IEntityTypeConfiguration<OperatorEntity>
{
    public void Configure(EntityTypeBuilder<OperatorEntity> builder)
    {
        builder.ToTable("Operators"); builder.HasKey(x => x.Id);
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired(); builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

public sealed class ThicknessFamilyConfiguration : IEntityTypeConfiguration<ThicknessFamilyEntity>
{
    public void Configure(EntityTypeBuilder<ThicknessFamilyEntity> builder)
    {
        builder.ToTable("ThicknessFamilies"); builder.HasKey(x => x.Id);
        builder.Property(x => x.MinimumIncomingThickness).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.MaximumIncomingThickness).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.ConventionalThickness).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.UsefulProductionThickness).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.StandardWidthReductionMillimeters).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.FingerJointLengthReductionMillimeters).HasColumnType(SqlPrecision.Dimension);
        builder.Property(x => x.RowVersion).IsRowVersion(); builder.HasIndex(x => x.ConventionalThickness).IsUnique();
    }
}

public sealed class ApplicationSettingsConfiguration : IEntityTypeConfiguration<ApplicationSettingsEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationSettingsEntity> builder)
    {
        builder.ToTable("ApplicationSettings"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.DefaultTimberCertification).HasMaxLength(40).IsRequired();
        builder.Property(x => x.StandardCubicMetersPerExpectedLoad23).HasColumnType(SqlPrecision.Volume);
        builder.Property(x => x.StandardCubicMetersPerExpectedLoad34).HasColumnType(SqlPrecision.Volume);
        builder.Property(x => x.StandardCubicMetersPerExpectedLoad44).HasColumnType(SqlPrecision.Volume);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
