using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace SMGS.Infrastructure.Services;

public static class MediaValidation
{
    public static long Limit(IConfiguration config, string category, long defaultMb) =>
        Math.Clamp(config.GetValue<long?>($"MediaLimits:{category}Mb") ?? defaultMb, 1, defaultMb) * 1024 * 1024;

    public static void Image(byte[] bytes, string extension)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        var format = extension.ToLowerInvariant() switch {
            ".png" => SKEncodedImageFormat.Png, ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp, _ => (SKEncodedImageFormat?)null
        };
        if (codec is null || format is null || codec.EncodedFormat != format || codec.Info.Width <= 0 || codec.Info.Height <= 0
            || codec.Info.Width > 8000 || codec.Info.Height > 8000 || (long)codec.Info.Width * codec.Info.Height > 20_000_000)
            throw new ArgumentException("Ảnh PNG/JPEG/WebP không hợp lệ hoặc vượt giới hạn 20 triệu pixel.");
        using var bitmap = new SKBitmap(codec.Info);
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new ArgumentException("File ảnh bị hỏng hoặc chưa đầy đủ.");
    }

    public static async Task Glb(Stream stream, long length, string name, string mime, long maxBytes, CancellationToken token)
    {
        if (!stream.CanSeek || length < 20 || length > maxBytes || stream.Length != length || Path.GetExtension(name).ToLowerInvariant() != ".glb"
            || mime is not ("model/gltf-binary" or "application/octet-stream"))
            throw new ArgumentException("Chỉ hỗ trợ GLB hợp lệ, tối đa dung lượng được cấu hình.");
        stream.Position = 0;
        try {
            var header = new byte[12]; await stream.ReadExactlyAsync(header, token);
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 0x46546C67 || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)) != 2
                || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)) != length)
                throw new ArgumentException("Header GLB không hợp lệ.");
            var chunk = new byte[8]; await stream.ReadExactlyAsync(chunk, token);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(chunk);
            if (BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4)) != 0x4E4F534A || size == 0 || size > 4 * 1024 * 1024 || size % 4 != 0 || size > length - 20)
                throw new ArgumentException("GLB JSON chunk không hợp lệ (tối đa 4 MB).");
            var json = new byte[size]; await stream.ReadExactlyAsync(json, token);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("asset", out var asset) || !asset.TryGetProperty("version", out var version) || version.GetString() != "2.0")
                throw new ArgumentException("GLB phải dùng glTF 2.0.");
            foreach (var field in new[] { "buffers", "images" })
                if (doc.RootElement.TryGetProperty(field, out var entries))
                    foreach (var entry in entries.EnumerateArray())
                        if (entry.TryGetProperty("uri", out _)) throw new ArgumentException("GLB phải nhúng dữ liệu, không được tham chiếu URI bên ngoài.");
            if (stream.Position < length) {
                await stream.ReadExactlyAsync(chunk, token);
                var binSize = BinaryPrimitives.ReadUInt32LittleEndian(chunk);
                if (BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4)) != 0x004E4942 || binSize % 4 != 0 || stream.Position + binSize != length)
                    throw new ArgumentException("GLB BIN chunk không hợp lệ.");
            }
        }
        catch (Exception ex) when (ex is JsonException or EndOfStreamException or InvalidOperationException) { throw new ArgumentException("File GLB bị hỏng."); }
        finally { stream.Position = 0; }
    }
}
