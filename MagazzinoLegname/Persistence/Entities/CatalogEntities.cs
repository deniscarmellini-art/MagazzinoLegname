namespace MagazzinoLegname.Persistence.Entities;

public sealed class SupplierEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? VatNumber { get; set; }
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? Country { get; set; }
    public string? Email { get; set; }
    public string? CertifiedEmail { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<SupplierContactEntity> Contacts { get; set; } = [];
    public ICollection<SupplierThicknessConfigurationEntity> ThicknessConfigurations { get; set; } = [];
    public ICollection<SupplierPriceEntity> Prices { get; set; } = [];
    public ICollection<LoadEntity> Loads { get; set; } = [];
}

public sealed class SupplierContactEntity
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public SupplierEntity Supplier { get; set; } = null!;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
}

public sealed class SupplierThicknessConfigurationEntity
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public SupplierEntity Supplier { get; set; } = null!;
    public decimal ConventionalThickness { get; set; }
    public bool IsPlaningEnabled { get; set; }
    public decimal PlaningReductionMillimeters { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class SupplierPriceEntity
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public SupplierEntity Supplier { get; set; } = null!;
    public decimal ConventionalThickness { get; set; }
    public decimal PricePerCubicMeter { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

public sealed class OperatorEntity
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class ThicknessFamilyEntity
{
    public Guid Id { get; set; }
    public decimal MinimumIncomingThickness { get; set; }
    public decimal MaximumIncomingThickness { get; set; }
    public decimal ConventionalThickness { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class ApplicationSettingsEntity
{
    public int Id { get; set; }
    public string DefaultTimberCertification { get; set; } = "PEFC";
    public decimal StandardCubicMetersPerExpectedLoad23 { get; set; }
    public decimal StandardCubicMetersPerExpectedLoad34 { get; set; }
    public decimal StandardCubicMetersPerExpectedLoad44 { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
