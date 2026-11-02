namespace SMGS.Application.Interfaces;

/// <summary>
/// Service sinh hình ảnh QR code từ nội dung chuỗi.
/// </summary>
public interface IQrCodeService
{
    /// <summary>
    /// Sinh ảnh QR code dạng byte[] (PNG) từ nội dung bất kỳ.
    /// </summary>
    /// <param name="content">Nội dung encode vào QR (thường là URL).</param>
    /// <param name="pixelsPerModule">Số pixel cho mỗi module QR (độ phân giải).</param>
    /// <returns>Mảng byte PNG.</returns>
    byte[] GeneratePng(string content, int pixelsPerModule = 20);

    /// <summary>
    /// Sinh ảnh QR code dạng Base64 string (PNG) từ nội dung bất kỳ.
    /// </summary>
    string GenerateBase64(string content, int pixelsPerModule = 20);
}
