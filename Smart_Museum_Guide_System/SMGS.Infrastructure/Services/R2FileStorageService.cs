using System.Net;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

public sealed class R2FileStorageService(IAmazonS3 client, IConfiguration configuration) : IFileStorageService
{
    private string Bucket => configuration["CloudflareR2:BucketName"] ?? "smgs-media";
    public static void ValidateKey(string key)
    {
        if (!Regex.IsMatch(key, @"^museums/[1-9]\d*/(?:images|artifacts/[1-9]\d*/(?:image|audio|video|models)|floor-maps/[1-9]\d*)/[a-f0-9]{32}\.[a-z0-9]+$"))
            throw new ArgumentException("Storage key không hợp lệ.");
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        try
        {
            await client.PutObjectAsync(new PutObjectRequest {
                BucketName = Bucket, Key = key, InputStream = content, ContentType = contentType,
                AutoCloseStream = false, DisablePayloadSigning = true, DisableDefaultChecksumValidation = true,
                UseChunkEncoding = false
            }, cancellationToken);
        }
        catch (AmazonS3Exception) { throw new MediaStorageException(); }
    }

    public async Task<StoredFileMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        try {
            var result = await client.GetObjectMetadataAsync(Bucket, key, cancellationToken);
            return new(result.ContentLength, result.Headers.ContentType);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound) { return null; }
        catch (AmazonS3Exception) { throw new MediaStorageException(); }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        try {
            var result = await client.GetObjectAsync(Bucket, key, cancellationToken);
            return new R2ReadStream(client, Bucket, key, result);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound) { throw new FileNotFoundException(); }
        catch (AmazonS3Exception) { throw new MediaStorageException(); }
    }

    public Task<string> GetDownloadUrlAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key); cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(client.GetPreSignedURL(new GetPreSignedUrlRequest {
            BucketName = Bucket, Key = key, Verb = HttpVerb.GET, Expires = DateTime.UtcNow.AddMinutes(5)
        }));
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        try { await client.DeleteObjectAsync(Bucket, key, cancellationToken); }
        catch (AmazonS3Exception) { throw new MediaStorageException(); }
    }
}
