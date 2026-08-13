using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class Supplier : ObservableObject
{
    private string _name;
    private bool _isActive;
    private string _vatNumber = string.Empty;
    private string _taxCode = string.Empty;
    private string _address = string.Empty;
    private string _postalCode = string.Empty;
    private string _city = string.Empty;
    private string _province = string.Empty;
    private string _country = "Italia";
    private string _email = string.Empty;
    private string _certifiedEmail = string.Empty;
    private string _code;

    public Supplier(Guid id, string name, bool isActive, string code = "NEW")
    {
        Id = id;
        _name = name;
        _isActive = isActive;
        _code = code.Trim().ToUpperInvariant();
        ThicknessConfigurations =
        [
            new SupplierThicknessConfiguration(id, 23m, false, 0m),
            new SupplierThicknessConfiguration(id, 34m, true, 5m),
            new SupplierThicknessConfiguration(id, 44m, true, 5m)
        ];
        Contacts = [];
    }

    public Guid Id { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Code { get => _code; set => SetProperty(ref _code, value.Trim().ToUpperInvariant()); }
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public string VatNumber { get => _vatNumber; set => SetProperty(ref _vatNumber, value); }
    public string TaxCode { get => _taxCode; set => SetProperty(ref _taxCode, value); }
    public string Address { get => _address; set => SetProperty(ref _address, value); }
    public string PostalCode { get => _postalCode; set => SetProperty(ref _postalCode, value); }
    public string City { get => _city; set => SetProperty(ref _city, value); }
    public string Province { get => _province; set => SetProperty(ref _province, value); }
    public string Country { get => _country; set => SetProperty(ref _country, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string CertifiedEmail { get => _certifiedEmail; set => SetProperty(ref _certifiedEmail, value); }
    public ObservableCollection<SupplierThicknessConfiguration> ThicknessConfigurations { get; }
    public ObservableCollection<SupplierContact> Contacts { get; }
}
