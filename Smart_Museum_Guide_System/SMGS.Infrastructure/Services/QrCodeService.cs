using QRCoder;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

/// <summary>
/// Implementation QR code service sử dụng thư viện QRCoder.
/// </summary>
public class QrCodeService : IQrCodeService
{
    public byte[] GeneratePng(string content, int pixelsPerModule = 20)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    public string GenerateBase64(string content, int pixelsPerModule = 20)
    {
        var pngBytes = GeneratePng(content, pixelsPerModule);
        return Convert.ToBase64String(pngBytes);
    }
}
