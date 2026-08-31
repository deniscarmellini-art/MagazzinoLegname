using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using MagazzinoLegname.Models;
using QRCoder;

namespace MagazzinoLegname.Services;

public sealed class QrCodeService
{
    public string BuildPayload(PhysicalPackageDraft package)
    {
        if (package.IsSupplementary)
            return BuildSupplementaryPayload(package.PackageCode, package.IncomingThickness,
                package.WidthAfterPlaning, package.IncomingLength, package.Quality, package.ArrivalDate);
        return string.Create(CultureInfo.InvariantCulture,
            $"ID={package.PackageCode}|TIPO=OFFICIAL|SP={package.IncomingThickness:0.##}|LA={package.IncomingWidth:0.##}|LU={package.IncomingLength:0.##}|PZ={package.PieceCount}|Q={package.Quality}|DATA={package.ArrivalDate:yyyy-MM-dd}");
    }

    public static string BuildSupplementaryPayload(string packageCode, MaterialGroupClassification group, DateTime arrivalDate) =>
        BuildSupplementaryPayload(packageCode, group.IncomingThickness, group.WidthAfterPlaning,
            group.IncomingLength, group.Quality, arrivalDate);

    private static string BuildSupplementaryPayload(string packageCode, decimal incomingThickness,
        decimal widthAfterPlaning, decimal incomingLength, string quality, DateTime arrivalDate) =>
        string.Create(CultureInfo.InvariantCulture,
            $"ID={packageCode}|TIPO=SUPPLEMENTARY|SP={incomingThickness:0.##}|LA={widthAfterPlaning:0.##}|LU={incomingLength:0.##}|Q={quality}|DATA={arrivalDate:yyyy-MM-dd}");

    public byte[] GenerateQrPng(string payload, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    public BitmapImage CreateWpfImage(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}