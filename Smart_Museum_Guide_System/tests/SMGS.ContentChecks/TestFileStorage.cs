using SMGS.Application.Interfaces;

internal sealed class TestFileStorage : IFileStorageService
{
    private readonly Dictionary<string, byte[]> files = [];
    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    { using var stream = new MemoryStream(); await content.CopyToAsync(stream, cancellationToken); files[key] = stream.ToArray(); }
    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream(files[key]));
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { files.Remove(key); return Task.CompletedTask; }
    public Task<StoredFileMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(files.TryGetValue(key, out var bytes) ? new StoredFileMetadata(bytes.Length, "application/octet-stream") : null);
    public Task<string> GetDownloadUrlAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
