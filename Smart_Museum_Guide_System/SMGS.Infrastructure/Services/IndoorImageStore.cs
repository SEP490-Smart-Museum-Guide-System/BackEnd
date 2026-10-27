using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SkiaSharp;
using SMGS.Application.DTOs.IndoorMap;

namespace SMGS.Infrastructure.Services;

public class IndoorImageStore(IHostEnvironment environment, IConfiguration configuration)
{
    private readonly string root = Path.GetFullPath(configuration["IndoorMaps:StoragePath"]
        ?? Path.Combine(environment.ContentRootPath, "App_Data", "indoor-maps"));

    public static (byte[] Bytes, int Width, int Height) Decode(byte[] bytes, string extension)
    {
        if (bytes.Length == 0 || bytes.Length > 10 * 1024 * 1024) throw new IndoorMapException("Ảnh phải có dung lượng từ 1 byte đến 10 MB.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg))
            throw new IndoorMapException("Nội dung file phải là ảnh PNG hoặc JPG hợp lệ.");
        if ((codec.EncodedFormat == SKEncodedImageFormat.Png && extension != ".png") ||
            (codec.EncodedFormat == SKEncodedImageFormat.Jpeg && extension is not (".jpg" or ".jpeg")))
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

    public async Task<(string Key, int Width, int Height)> Save(byte[] bytes, string extension)
    {
        var decoded = Decode(bytes, extension.ToLowerInvariant());
        Directory.CreateDirectory(root);
        var key = Guid.NewGuid().ToString("N") + ".png";
        await File.WriteAllBytesAsync(Path.Combine(root, key), decoded.Bytes);
        return (key, decoded.Width, decoded.Height);
    }

    public string PathFor(string key)
    {
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _) || Path.GetFileName(key) != key || Path.GetExtension(key) != ".png")
            throw new IndoorMapException("Đường dẫn ảnh không hợp lệ.", 404);
        return Path.Combine(root, key);
    }

    public void Delete(string key) => File.Delete(PathFor(key));
}
