using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using MagazzinoLegname.Models;
using QRCoder;

namespace MagazzinoLegname.Services;

public sealed class QrCodeService
{
    public string BuildPayload(PhysicalPackageDraft package) => string.Create(CultureInfo.InvariantCulture,
        $"ID={package.PackageCode}|SP={package.IncomingThickness:0.##}|LA={package.IncomingWidth:0.##}|LU={package.IncomingLength:0.##}|PZ={package.PieceCount}|Q={package.Quality}|DATA={package.ArrivalDate:yyyy-MM-dd}");

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
