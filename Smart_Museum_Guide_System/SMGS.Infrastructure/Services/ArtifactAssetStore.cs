using SMGS.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace SMGS.Infrastructure.Services;

public class ArtifactAssetStore(IConfiguration configuration, IServiceProvider? services = null)
{
    private IFileStorageService Storage => services?.GetRequiredService<IFileStorageService>() ?? throw new MediaStorageException();

    private static readonly Dictionary<string, (string MediaType, string ContentType, long MaxBytes)> SupportedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = ("image", "image/png", 15L * 1024 * 1024),
        [".jpg"] = ("image", "image/jpeg", 15L * 1024 * 1024),
        [".jpeg"] = ("image", "image/jpeg", 15L * 1024 * 1024),
        [".webp"] = ("image", "image/webp", 15L * 1024 * 1024),
        [".mp3"] = ("audio", "audio/mpeg", 50L * 1024 * 1024),
        [".wav"] = ("audio", "audio/wav", 50L * 1024 * 1024),
        [".ogg"] = ("audio", "audio/ogg", 50L * 1024 * 1024),
        [".m4a"] = ("audio", "audio/mp4", 50L * 1024 * 1024),
        [".mp4"] = ("video", "video/mp4", 100L * 1024 * 1024),
        [".webm"] = ("video", "video/webm", 100L * 1024 * 1024)
    };

    public static (string MediaType, string ContentType, string Extension) ValidateMedia(
        byte[] bytes, string originalFileName, string? requestedMediaType = null)
    {
        if (bytes is null || bytes.Length == 0)
            throw new ArgumentException("File media không được rỗng.");

        var ext = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        if (!SupportedMediaExtensions.TryGetValue(ext, out var spec))
            throw new ArgumentException("Định dạng media không được hỗ trợ. Hỗ trợ: .png, .jpg, .jpeg, .webp, .mp3, .wav, .ogg, .m4a, .mp4, .webm.");

        if (bytes.Length > spec.MaxBytes)
            throw new ArgumentException($"Kích thước file vượt quá giới hạn cho phép ({spec.MaxBytes / (1024 * 1024)} MB).");

        if (!string.IsNullOrWhiteSpace(requestedMediaType) &&
            !string.Equals(requestedMediaType.Trim(), spec.MediaType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Loại media '{requestedMediaType}' không khớp với đuôi file '{ext}'.");

        if (spec.MediaType == "image") MediaValidation.Image(bytes, ext);
        return (spec.MediaType, spec.ContentType, ext);
    }

    public static (string Format, string ContentType, string Extension) Validate3DModel(byte[] bytes, string originalFileName)
    {
        using var stream = new MemoryStream(bytes);
        MediaValidation.Glb(stream, bytes.LongLength, originalFileName, "model/gltf-binary", 100L * 1024 * 1024, default).GetAwaiter().GetResult();
        return ("glb", "model/gltf-binary", ".glb");
    }

    public async Task<(string StorageKey, string MediaType, string ContentType)> SaveMediaAsync(
        byte[] bytes, string originalFileName, string? requestedMediaType, int museumId, int artifactId, CancellationToken token = default)
    {
        var (mediaType, contentType, ext) = ValidateMedia(bytes, originalFileName, requestedMediaType);
        if (bytes.LongLength > MediaValidation.Limit(configuration, mediaType, mediaType == "image" ? 15 : mediaType == "audio" ? 50 : 100))
            throw new ArgumentException("File vượt giới hạn dung lượng cấu hình.");
        var key = $"museums/{museumId}/artifacts/{artifactId}/{mediaType}/{Guid.NewGuid():N}{ext}";
        using var stream = new MemoryStream(bytes, false);
        await Storage.UploadAsync(key, stream, contentType, token);
        return (key, mediaType, contentType);
    }

    public async Task<(string StorageKey, string Format, string ContentType)> Save3DModelAsync(
        Stream stream, long length, string originalFileName, string mime, int museumId, int artifactId, CancellationToken token = default)
    {
        await MediaValidation.Glb(stream, length, originalFileName, mime, MediaValidation.Limit(configuration, "Model", 100), token);
        var key = $"museums/{museumId}/artifacts/{artifactId}/models/{Guid.NewGuid():N}.glb";
        await Storage.UploadAsync(key, stream, "model/gltf-binary", token);
        return (key, "glb", "model/gltf-binary");
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken token = default) => await Storage.OpenReadAsync(key, token);

    public async Task DeleteIfExists(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        // Legacy files may be shared by old records; preserve originals during transition.
        if (!key.StartsWith("museums/")) return;
        try { await Storage.DeleteAsync(key); }
        catch (Exception ex) when (ex is MediaStorageException or HttpRequestException or TaskCanceledException) { /* shortcut: retain failed cleanup for manual reconciliation; add a retry queue if orphan volume grows. */ }
    }


}
