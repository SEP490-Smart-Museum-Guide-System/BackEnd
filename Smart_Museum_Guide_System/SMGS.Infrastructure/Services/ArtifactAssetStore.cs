using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SMGS.Infrastructure.Services;

public class ArtifactAssetStore(IHostEnvironment environment, IConfiguration configuration)
{
    private readonly string root = Path.GetFullPath(configuration["ArtifactAssets:StoragePath"]
        ?? Path.Combine(environment.ContentRootPath, "App_Data", "artifact-assets"));

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

        return (spec.MediaType, spec.ContentType, ext);
    }

    public static (string Format, string ContentType, string Extension) Validate3DModel(
        byte[] bytes, string originalFileName)
    {
        if (bytes is null || bytes.Length == 0)
            throw new ArgumentException("File mô hình 3D không được rỗng.");

        if (bytes.Length > 100L * 1024 * 1024)
            throw new ArgumentException("File mô hình 3D tối đa 100 MB.");

        var ext = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        if (ext == ".glb")
        {
            // GLB header: magic 'glTF' (0x67, 0x6C, 0x54, 0x46) + version (4 bytes) + length (4 bytes) >= 12 bytes
            if (bytes.Length < 12 || bytes[0] != 0x67 || bytes[1] != 0x6C || bytes[2] != 0x54 || bytes[3] != 0x46)
                throw new ArgumentException("Nội dung file .glb không hợp lệ (thiếu header glTF binary).");
            return ("glb", "model/gltf-binary", ".glb");
        }

        if (ext == ".gltf")
        {
            try
            {
                using var doc = JsonDocument.Parse(bytes);
                if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("asset", out _))
                    throw new ArgumentException("Nội dung file .gltf không hợp lệ (thiếu thuộc tính asset).");
            }
            catch (JsonException)
            {
                throw new ArgumentException("Nội dung file .gltf không phải JSON hợp lệ.");
            }
            return ("gltf", "model/gltf+json", ".gltf");
        }

        throw new ArgumentException("Chỉ hỗ trợ mô hình 3D định dạng .glb hoặc .gltf.");
    }

    public async Task<(string StorageKey, string MediaType, string ContentType)> SaveMediaAsync(
        byte[] bytes, string originalFileName, string? requestedMediaType = null)
    {
        var (mediaType, contentType, ext) = ValidateMedia(bytes, originalFileName, requestedMediaType);
        Directory.CreateDirectory(root);
        var key = Guid.NewGuid().ToString("N") + ext;
        await File.WriteAllBytesAsync(Path.Combine(root, key), bytes);
        return (key, mediaType, contentType);
    }

    public async Task<(string StorageKey, string Format, string ContentType)> Save3DModelAsync(
        byte[] bytes, string originalFileName)
    {
        var (format, contentType, ext) = Validate3DModel(bytes, originalFileName);
        Directory.CreateDirectory(root);
        var key = Guid.NewGuid().ToString("N") + ext;
        await File.WriteAllBytesAsync(Path.Combine(root, key), bytes);
        return (key, format, contentType);
    }

    public string PathFor(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey)
            throw new ArgumentException("Đường dẫn file không hợp lệ.");

        var nameWithoutExt = Path.GetFileNameWithoutExtension(storageKey);
        if (!Guid.TryParseExact(nameWithoutExt, "N", out _))
            throw new ArgumentException("Đường dẫn file không hợp lệ.");

        return Path.Combine(root, storageKey);
    }

    public void DeleteIfExists(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;
        try
        {
            var path = PathFor(storageKey);
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Ignore cleanup errors for missing/invalid keys.
        }
    }
}

