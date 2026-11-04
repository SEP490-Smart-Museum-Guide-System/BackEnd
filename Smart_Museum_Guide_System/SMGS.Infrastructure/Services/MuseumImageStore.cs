using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMGS.Application.Interfaces;
using SkiaSharp;

namespace SMGS.Infrastructure.Services;

public class MuseumImageStore(IConfiguration configuration, IServiceProvider services)
{
    public const int MaxBytes = 15 * 1024 * 1024;
    private IFileStorageService Storage => services.GetRequiredService<IFileStorageService>();

    public string ValidateUpload(byte[] bytes, string fileName)
    {
        var extension = Validate(bytes, fileName);
        if (bytes.LongLength > MediaValidation.Limit(configuration, "image", 15))
            throw new ArgumentException("Ảnh vượt giới hạn dung lượng cấu hình.");
        return extension;
    }

    public static string Validate(byte[] bytes, string fileName)
    {
        if (bytes.Length == 0 || bytes.Length > MaxBytes)
            throw new ArgumentException("Ảnh phải có dung lượng từ 1 byte đến 15 MB.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        var valid = codec is not null && (codec.EncodedFormat, extension) switch
        {
            (SKEncodedImageFormat.Png, ".png") => true,
            (SKEncodedImageFormat.Jpeg, ".jpg" or ".jpeg") => true,
            (SKEncodedImageFormat.Webp, ".webp") => true,
            _ => false,
        };
        if (!valid) throw new ArgumentException("Chọn ảnh PNG, JPEG hoặc WebP hợp lệ; đuôi file phải khớp nội dung ảnh.");
        var info = codec!.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 8000 || info.Height > 8000 || (long)info.Width * info.Height > 20_000_000)
            throw new ArgumentException("Ảnh tối đa 8.000 pixel mỗi chiều và 20 triệu pixel.");
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new ArgumentException("Ảnh bị hỏng hoặc chưa đầy đủ.");
        return extension;
    }

    public async Task<string> Save(byte[] bytes, string fileName, int museumId, CancellationToken cancellationToken = default)
    {
        var extension = ValidateUpload(bytes, fileName);
        var key = $"museums/{museumId}/images/{Guid.NewGuid():N}{extension}";
        R2FileStorageService.ValidateKey(key);
        using var stream = new MemoryStream(bytes, false);
        await Storage.UploadAsync(key, stream, ContentType(key), cancellationToken);
        return key;
    }

    public static string ContentType(string key) => Path.GetExtension(key) switch
    {
        ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg",
    };

    public async Task Delete(string? key)
    {
        if (key is null) return;
        try { await Storage.DeleteAsync(key, CancellationToken.None); }
        catch (Exception error) when (error is MediaStorageException or HttpRequestException or OperationCanceledException) { /* Preserve the original save error; orphan cleanup can be retried. */ }
    }
}
