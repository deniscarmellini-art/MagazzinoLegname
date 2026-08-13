using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class SupplierContact : ObservableObject
{
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _role = string.Empty;
    private string _phone = string.Empty;
    private string _mobile = string.Empty;
    private string _email = string.Empty;

    public Guid Id { get; init; } = Guid.NewGuid();
    public string FirstName { get => _firstName; set => SetProperty(ref _firstName, value); }
    public string LastName { get => _lastName; set => SetProperty(ref _lastName, value); }
    public string Role { get => _role; set => SetProperty(ref _role, value); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
    public string Mobile { get => _mobile; set => SetProperty(ref _mobile, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
}
