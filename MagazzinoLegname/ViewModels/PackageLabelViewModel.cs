using System.Windows.Media.Imaging;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class PackageLabelViewModel
{
    public const double A4LandscapeWidth = 1122.52;
    public const double A4LandscapeHeight = 793.70;

    public PackageLabelViewModel(PhysicalPackageDraft package, string supplierName,
        string loadNumber, string deliveryNoteNumber, string certification,
        QrCodeService qrCodeService)
    {
        Package = package;
        SupplierName = supplierName;
        LoadNumber = loadNumber;
        DeliveryNoteNumber = deliveryNoteNumber;
        Certification = certification;
        QrImage = qrCodeService.CreateWpfImage(qrCodeService.GenerateQrPng(package.QrPayload, 12));
    }

    public PhysicalPackageDraft Package { get; }
    public string SupplierName { get; }
    public string LoadNumber { get; }
    public string DeliveryNoteNumber { get; }
    public string DisplayDeliveryNoteNumber => string.IsNullOrWhiteSpace(DeliveryNoteNumber) ? "—" : DeliveryNoteNumber;
    public string Certification { get; }
    public BitmapImage QrImage { get; }
    public string PackagePosition => $"{Package.SequenceNumber} / {Package.TotalPackages}";
    public string OperationalMeasure =>
        $"{Package.IncomingThickness:0.##} × {Package.WidthAfterPlaning:0.##} × {Package.IncomingLength:0.##}";
}
