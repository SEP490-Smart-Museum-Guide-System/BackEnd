using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using SkiaSharp;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Artifact;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Services;

await ScanChecks.Run();
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static async Task Throws<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
static byte[] Glb(string json = "{\"asset\":{\"version\":\"2.0\"}}") {
    json = json.PadRight((json.Length + 3) / 4 * 4);
    using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
    writer.Write(0x46546C67); writer.Write(2); writer.Write(20 + json.Length); writer.Write(json.Length); writer.Write(0x4E4F534A); writer.Write(Encoding.UTF8.GetBytes(json));
    return stream.ToArray();
}
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["CloudflareR2:BucketName"] = "smgs-media" }).Build();
var memory = new MemoryStorage();
using var provider = new ServiceCollection().AddSingleton<IFileStorageService>(memory).BuildServiceProvider();
var store = new ArtifactAssetStore(config, provider);
using var bitmap = new SKBitmap(2, 2); bitmap.Erase(SKColors.Red);
using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
var png = encoded.ToArray();
Check(ArtifactAssetStore.ValidateMedia(png, "ok.png").MediaType == "image", "Valid PNG");
await Throws<ArgumentException>(() => Task.Run(() => ArtifactAssetStore.ValidateMedia("fake"u8.ToArray(), "fake.png")));
await Throws<ArgumentException>(() => Task.Run(() => ArtifactAssetStore.ValidateMedia(png, "wrong.jpg")));
await Throws<ArgumentException>(() => Task.Run(() => ArtifactAssetStore.ValidateMedia([], "empty.png")));
await Throws<ArgumentException>(() => Task.Run(() => ArtifactAssetStore.ValidateMedia(new byte[16 * 1024 * 1024], "large.png")));
var glb = Glb();
await MediaValidation.Glb(new MemoryStream(glb), glb.Length, "ok.glb", "model/gltf-binary", 100000, default);
var external = Glb("{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"https://evil/file\"}]}");
await Throws<ArgumentException>(() => MediaValidation.Glb(new MemoryStream(external), external.Length, "bad.glb", "model/gltf-binary", 100000, default));
await Throws<ArgumentException>(() => MediaValidation.Glb(new MemoryStream(glb), glb.Length, "bad.gltf", "model/gltf+json", 100000, default));
await Throws<ArgumentException>(() => MediaValidation.Glb(new MemoryStream(glb), glb.Length, "ok.glb", "image/png", 100000, default));
await Throws<ArgumentException>(() => MediaValidation.Glb(new MemoryStream(glb), glb.Length, "ok.glb", "model/gltf-binary", 10, default));
await Throws<OperationCanceledException>(() => store.Save3DModelAsync(new MemoryStream(glb), glb.Length, "ok.glb", "model/gltf-binary", 1, 1, new CancellationToken(true)));
var fail = new FailSave();
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(fail).Options);
db.Artifacts.Add(new Artifact { Id = 1, MuseumId = 1, Name = "Artifact", AccessLevel = "free" });
var staff = Guid.NewGuid(); db.StaffAssignments.Add(new StaffAssignment { Id = 1, MuseumId = 2, UserId = staff }); await db.SaveChangesAsync();
var service = new ArtifactAssetService(db, store);
Check((await service.UploadMediaAsync(1, png, "ok.png", null, null, null, 0, staff, false)).StatusCode == 403, "Cross museum denied");
Check(memory.Files.Count == 0, "Denied upload never touches storage");
var uploaded = await service.UploadMediaAsync(1, png, "../safe.png", null, null, null, 0, staff, true);
Check(uploaded.StatusCode == 201 && memory.Files.Count == 1, "Image upload");
var media = await db.ArtifactMedias.SingleAsync(); Check(media.StorageKey.StartsWith("museums/1/artifacts/1/image/"), "Scoped generated key");
var read = await service.GetMediaFileAsync(1, media.Id); using (read.Data.Content) Check(read.Data.Content.Length == png.Length, "Retrieval");
async Task<int> Model() => (await service.UploadOrReplace3DModelAsync(1, new MemoryStream(glb), glb.Length, "model/gltf-binary", "model.glb", new Update3DModelSettingsRequestDto(), staff, true)).StatusCode;
Check(await Model() == 201, "GLB upload"); var oldKey = (await db.Artifact3DModels.SingleAsync()).StorageKey;
Check(await Model() == 200 && !memory.Files.ContainsKey(oldKey), "Replacement deletes unreferenced old key");
var shared = new ArtifactMedia { ArtifactId = 1, StorageKey = media.StorageKey, FileName = "shared.png", ContentType = "image/png", MediaType = "image", Title = "shared" };
db.ArtifactMedias.Add(shared); await db.SaveChangesAsync();
await service.DeleteMediaAsync(1, media.Id, staff, true); Check(memory.Files.ContainsKey(media.StorageKey), "Shared media preserved");
await service.DeleteMediaAsync(1, shared.Id, staff, true); Check(!memory.Files.ContainsKey(media.StorageKey), "Unreferenced media deleted");
var count = memory.Files.Count; fail.Enabled = true;
await Throws<DbUpdateException>(() => service.UploadMediaAsync(1, png, "ok.png", null, null, null, 0, staff, true));
Check(memory.Files.Count == count, "DB failure rolls back uploaded object"); fail.Enabled = false; db.ChangeTracker.Clear();
memory.Fail = true; await Throws<MediaStorageException>(() => service.UploadMediaAsync(1, png, "ok.png", null, null, null, 0, staff, true)); memory.Fail = false;
var controller = new ArtifactAssetsController(service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
Check(await controller.UploadMedia(1, null) is UnauthorizedObjectResult, "Unauthenticated controller request");
foreach (var method in new[] { "UploadMedia", "UploadOrReplace3DModel", "DeleteMedia", "Delete3DModel" })
    Check(typeof(ArtifactAssetsController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>() is not null, "Protected mutation " + method);
db.Museums.Add(new Museum { Id = 1, Name = "Museum", IsActive = true }); await db.SaveChangesAsync();
var http = new DefaultHttpContext(); http.Request.Scheme = "https"; http.Request.Host = new HostString("museum.test");
http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, staff.ToString()), new Claim(ClaimTypes.Role, "Admin")], "test"));
var museumImages = new MuseumImagesController(db, provider, config) { ControllerContext = new ControllerContext { HttpContext = http } };
Check(await museumImages.Upload(1, null, default) is BadRequestObjectResult, "Museum missing file");
IFormFile ImageFile() => new FormFile(new MemoryStream(png), 0, png.Length, "file", "cover.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
Check(await museumImages.Upload(1, ImageFile(), default) is OkObjectResult, "Museum upload");
var cover = (await db.Museums.SingleAsync()).CoverImageUrl!; var coverName = cover.Split('/').Last();
Check(cover.StartsWith("https://museum.test/api/museums/1/image/") && !cover.Contains('?'), "Stable museum URL, not presigned");
var coverResult = (FileStreamResult)await museumImages.Image(1, coverName, default); await coverResult.FileStream.DisposeAsync();
Check(await museumImages.Image(2, coverName, default) is NotFoundResult, "Cross museum image denied");
Check(await museumImages.Upload(1, ImageFile(), default) is OkObjectResult, "Museum replacement");
Check(memory.Files.ContainsKey($"museums/1/images/{coverName}"), "Retain potentially shared prior museum image");
Check(await museumImages.Remove(1, default) is OkObjectResult && (await db.Museums.SingleAsync()).CoverImageUrl is null, "Museum unlink");
http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, staff.ToString()), new Claim(ClaimTypes.Role, "Museum Staff")], "test"));
Check(await museumImages.Upload(1, ImageFile(), default) is StatusCodeResult { StatusCode: 403 }, "Unassigned museum staff denied");
var indoor = new IndoorImageStore(config, provider);
var plan = await indoor.Save(png, ".png", 1, 2);
Check(plan.Key.StartsWith("museums/1/floor-maps/2/") && plan.Width == 2 && plan.Height == 2, "Floor map upload");
using (var planStream = await indoor.OpenRead(plan.Key)) Check(planStream.Length > 0, "Floor map retrieval");
await indoor.Delete(plan.Key); Check(!memory.Files.ContainsKey(plan.Key), "Floor map rollback cleanup");
using var fake = new FakeS3(); var r2 = new R2FileStorageService(fake, config);
using var r2Provider = new ServiceCollection().AddSingleton<IFileStorageService>(r2).BuildServiceProvider();
await Throws<ArgumentException>(() => new ArtifactAssetStore(config, r2Provider).OpenReadAsync("126eb876d11744f58abcbc6a1453910d.wav"));
await Throws<ArgumentException>(() => new IndoorImageStore(config, r2Provider).OpenRead("126eb876d11744f58abcbc6a1453910d.png"));
var key = $"museums/1/images/{Guid.NewGuid():N}.png";
await r2.UploadAsync(key, new MemoryStream(png), "image/png");
Check(fake.Put is { BucketName: "smgs-media", DisablePayloadSigning: true, DisableDefaultChecksumValidation: true, UseChunkEncoding: false }, "R2 signing compatibility");
Check((await r2.GetMetadataAsync(key))!.Length == 3, "Metadata/existence");
using (var stream = await r2.OpenReadAsync(key)) { Check(stream.ReadByte() == 1, "R2 retrieval"); stream.Seek(2, SeekOrigin.Begin); Check(stream.ReadByte() == 3, "R2 range retrieval"); }
await r2.DeleteAsync(key); Check(fake.Deleted == key, "R2 deletion");
fake.Fail = true;
await Throws<MediaStorageException>(() => r2.UploadAsync(key, new MemoryStream(png), "image/png"));
await Throws<ArgumentException>(() => r2.OpenReadAsync("museums/1/../../secret"));
await Throws<OperationCanceledException>(() => r2.UploadAsync(key, new MemoryStream(png), "image/png", new CancellationToken(true)));
Console.WriteLine("PASS: image/GLB validation, scoped uploads, permissions, reads, replacement, shared deletion, DB rollback, storage errors, R2 requests, invalid credentials and cancellation (mock, no live R2).");

class FailSave : SaveChangesInterceptor {
    public bool Enabled;
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        if (Enabled) throw new DbUpdateException("Simulated database failure"); return ValueTask.FromResult(result);
    }
}
class MemoryStorage : IFileStorageService {
    public Dictionary<string, byte[]> Files = []; public bool Fail;
    public async Task UploadAsync(string key, Stream stream, string type, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); if (Fail) throw new MediaStorageException(); using var b = new MemoryStream(); await stream.CopyToAsync(b, cancellationToken); Files[key] = b.ToArray(); }
    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream(Files[key]));
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { Files.Remove(key); return Task.CompletedTask; }
    public Task<StoredFileMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(Files.TryGetValue(key, out var data) ? new StoredFileMetadata(data.Length, "image/png") : null);
    public Task<string> GetDownloadUrlAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
class FakeS3 : AmazonS3Client {
    public PutObjectRequest? Put; public string? Deleted; public bool Fail;
    public FakeS3() : base("mock", "mock", new AmazonS3Config { ServiceURL = "https://example.r2.cloudflarestorage.com" }) { }
    public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken token = default) { token.ThrowIfCancellationRequested(); if (Fail) throw new AmazonS3Exception("secret provider detail") { StatusCode = HttpStatusCode.Forbidden }; Put = request; return Task.FromResult(new PutObjectResponse()); }
    public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucket, string key, CancellationToken token = default) => Task.FromResult(new GetObjectMetadataResponse { ContentLength = 3 });
    public override Task<GetObjectResponse> GetObjectAsync(string bucket, string key, CancellationToken token = default) => Task.FromResult(new GetObjectResponse { ContentLength = 3, ResponseStream = new MemoryStream([1, 2, 3]) });
    public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken token = default) => Task.FromResult(new GetObjectResponse { ContentLength = 1, ResponseStream = new MemoryStream([3]) });
    public override Task<DeleteObjectResponse> DeleteObjectAsync(string bucket, string key, CancellationToken token = default) { Deleted = key; return Task.FromResult(new DeleteObjectResponse()); }
}
class TestEnvironment : IHostEnvironment {
    public string EnvironmentName { get; set; } = "Test"; public string ApplicationName { get; set; } = "Test";
    public string ContentRootPath { get; set; } = Path.GetTempPath(); public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
