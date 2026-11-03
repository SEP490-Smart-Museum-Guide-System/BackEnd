using Amazon.S3;
using Amazon.S3.Model;

namespace SMGS.Infrastructure.Services;

// Allows MVC range delivery without buffering an entire model, audio or video file.
internal sealed class R2ReadStream(IAmazonS3 client, string bucket, string key, GetObjectResponse initial) : Stream
{
    private GetObjectResponse? response = initial;
    private long position;
    private long responsePosition;
    private readonly long length = initial.ContentLength;
    private bool disposed;
    public override bool CanRead => !disposed;
    public override bool CanSeek => !disposed;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var target = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => checked(position + offset), SeekOrigin.End => checked(length + offset), _ => throw new ArgumentOutOfRangeException(nameof(origin)) };
        if (target < 0 || target > length) throw new ArgumentOutOfRangeException(nameof(offset));
        return position = target;
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (position == length || buffer.IsEmpty) return 0;
        if (responsePosition != position) {
            response?.Dispose(); response = null;
            response = await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key, ByteRange = new ByteRange(position, length - 1) }, cancellationToken);
            responsePosition = position;
        }
        var read = await response!.ResponseStream.ReadAsync(buffer[..(int)Math.Min(buffer.Length, length - position)], cancellationToken);
        position += read; responsePosition += read;
        return read;
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) { disposed = true; response?.Dispose(); } base.Dispose(disposing); }
}
