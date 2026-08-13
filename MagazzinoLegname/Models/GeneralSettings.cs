using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class GeneralSettings : ObservableObject
{
    private string _defaultTimberCertification = "PEFC";
    public string DefaultTimberCertification
    {
        get => _defaultTimberCertification;
        set => SetProperty(ref _defaultTimberCertification, string.IsNullOrWhiteSpace(value) ? "PEFC" : value.Trim().ToUpperInvariant());
    }
}
