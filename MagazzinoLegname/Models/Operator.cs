using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class Operator : ObservableObject
{
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private bool _isActive = true;

    public Guid Id { get; init; } = Guid.NewGuid();
    public string FirstName
    {
        get => _firstName;
        set
        {
            if (SetProperty(ref _firstName, value?.Trim() ?? string.Empty))
                OnPropertyChanged(nameof(DisplayName));
        }
    }
    public string LastName
    {
        get => _lastName;
        set
        {
            if (SetProperty(ref _lastName, value?.Trim() ?? string.Empty))
                OnPropertyChanged(nameof(DisplayName));
        }
    }
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(ToggleActionLabel));
            }
        }
    }

    public string DisplayName => $"{FirstName} {LastName}".Trim();
    public string Status => IsActive ? "Attivo" : "Inattivo";
    public string ToggleActionLabel => IsActive ? "Disattiva" : "Riattiva";
}
