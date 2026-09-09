using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class GeneralSettings : ObservableObject
{
    public byte[] RowVersion { get; internal set; } = [];
    private string _defaultTimberCertification = "PEFC";
    public string DefaultTimberCertification
    {
        get => _defaultTimberCertification;
        set => SetProperty(ref _defaultTimberCertification, string.IsNullOrWhiteSpace(value) ? "PEFC" : value.Trim().ToUpperInvariant());
    }
}
