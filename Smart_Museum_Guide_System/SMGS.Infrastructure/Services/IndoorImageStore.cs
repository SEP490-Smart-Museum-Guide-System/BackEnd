using SMGS.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using SMGS.Application.DTOs.IndoorMap;

namespace SMGS.Infrastructure.Services;

public class IndoorImageStore(IConfiguration configuration, IServiceProvider? services = null)
{
    private IFileStorageService Storage => services?.GetRequiredService<IFileStorageService>() ?? throw new MediaStorageException();

    public static (byte[] Bytes, int Width, int Height) Decode(byte[] bytes, string extension)
    {
        if (bytes.Length == 0 || bytes.Length > 10 * 1024 * 1024) throw new IndoorMapException("Ảnh phải có dung lượng từ 1 byte đến 10 MB.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))
            throw new IndoorMapException("Nội dung file phải là ảnh PNG, JPG hoặc WebP hợp lệ.");
        if ((codec.EncodedFormat == SKEncodedImageFormat.Png && extension != ".png") ||
            (codec.EncodedFormat == SKEncodedImageFormat.Jpeg && extension is not (".jpg" or ".jpeg")) ||
            (codec.EncodedFormat == SKEncodedImageFormat.Webp && extension != ".webp"))
            throw new IndoorMapException("Đuôi file không khớp nội dung ảnh.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 8000 || info.Height > 8000 || (long)info.Width * info.Height > 20_000_000)
            throw new IndoorMapException("Ảnh tối đa 8.000 pixel mỗi chiều và 20 triệu pixel.");
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new IndoorMapException("Ảnh bị hỏng hoặc chưa đầy đủ.");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return (encoded.ToArray(), info.Width, info.Height);
    }

    public async Task<(string Key, int Width, int Height)> Save(byte[] bytes, string extension, int museumId, int mapId, CancellationToken token = default)
    {
        var decoded = Decode(bytes, extension.ToLowerInvariant());
        if (bytes.LongLength > MediaValidation.Limit(configuration, "FloorPlan", 10)) throw new IndoorMapException("Ảnh vượt giới hạn cấu hình.");
        var key = $"museums/{museumId}/floor-maps/{mapId}/{Guid.NewGuid():N}.png";
        using var stream = new MemoryStream(decoded.Bytes, false);
        await Storage.UploadAsync(key, stream, "image/png", token);
        return (key, decoded.Width, decoded.Height);
    }


    public async Task<Stream> OpenRead(string key, CancellationToken token = default) => await Storage.OpenReadAsync(key, token);
    public async Task Delete(string key)
    {
        if (!key.StartsWith("museums/")) return;
        try { await Storage.DeleteAsync(key); }
        catch (Exception ex) when (ex is MediaStorageException or HttpRequestException or TaskCanceledException) { /* shortcut: retain failed cleanup for manual reconciliation; add a retry queue if orphan volume grows. */ }
    }
}
