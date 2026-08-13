using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class QrPackagePreviewViewModel
{
    private readonly QrCodeService _qrCodeService = new();
    public QrPackagePreviewViewModel(IEnumerable<PhysicalPackageDraft> packages,
        string supplierName, string loadNumber)
    {
        Packages = new(packages.Select(package => new QrPackagePreviewItem(package,
            supplierName, loadNumber, _qrCodeService.CreateWpfImage(
                _qrCodeService.GenerateQrPng(package.QrPayload)))));
    }
    public ObservableCollection<QrPackagePreviewItem> Packages { get; }
}

public sealed record QrPackagePreviewItem(PhysicalPackageDraft Package, string SupplierName,
    string LoadNumber, BitmapImage QrImage);
