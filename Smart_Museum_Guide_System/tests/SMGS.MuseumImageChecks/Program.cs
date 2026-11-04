using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using SMGS.API.Controllers;
using SMGS.Application.Interfaces;
using SMGS.Application.Services;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Services;
using SMGS.Infrastructure.Repositories;

static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
using var bitmap = new SKBitmap(8, 8); bitmap.Erase(SKColors.Maroon);
using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
var png = encoded.ToArray();
Check(MuseumImageStore.Validate(png, "photo.png") == ".png", "PNG must decode");
foreach (var item in new[] { (Array.Empty<byte>(), "a.png"), (new byte[16 * 1024 * 1024], "a.png"), (png, "a.jpg"), (System.Text.Encoding.UTF8.GetBytes("<svg/>"), "a.png") }) {
    try { MuseumImageStore.Validate(item.Item1, item.Item2); throw new Exception("Invalid image accepted"); }
    catch (ArgumentException) { }
}
var database = "museum-forms-" + Guid.NewGuid().ToString("N");
var storage = new MemoryStorage();
var failure = new FailSave();
void ConfigureDb(DbContextOptionsBuilder options) => options.UseInMemoryDatabase(database)
    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).AddInterceptors(failure);
var inspectionOptions = new DbContextOptionsBuilder<ApplicationDbContext>(); ConfigureDb(inspectionOptions);
using var inspection = new ApplicationDbContext(inspectionOptions.Options);
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddScoped<IMuseumRepository, MuseumRepository>();
builder.Services.AddSingleton<IFileStorageService>(storage);
builder.Services.AddScoped<IMuseumService, MuseumService>();
builder.Services.AddSingleton<MuseumImageStore>();
// Real service/repository and HTTP with isolated EF InMemory + storage fixtures; no PostgreSQL/R2 writes.
builder.Services.AddDbContext<ApplicationDbContext>(ConfigureDb);
builder.Services.AddControllers().AddApplicationPart(typeof(MuseumImagesController).Assembly);
builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
builder.Services.AddAuthorization();
await using var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
await app.StartAsync();
try {
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var client = new HttpClient { BaseAddress = new Uri(address) };
    async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? role, byte[]? image, string name = "Bảo tàng ảnh", string filename = "museum.png") {
        using var request = new HttpRequestMessage(method, path);
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        if (method != HttpMethod.Delete) {
            var body = new MultipartFormDataContent();
            body.Add(new StringContent(name), "name"); body.Add(new StringContent("79"), "cityCode");
            body.Add(new StringContent("Địa chỉ bảo tàng"), "address");
            if (image is not null) body.Add(new ByteArrayContent(image), "file", filename);
            request.Content = body;
        }
        return await client.SendAsync(request);
    }
    using var anonymous = await Send(HttpMethod.Post, "/api/museums/with-image", null, png);
    Check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "Anonymous upload must fail");
    foreach (var role in new[] { "Visitor", "Curator", "Staff", "Museum Staff" }) {
        using var denied = await Send(HttpMethod.Post, "/api/museums/with-image", role, png);
        Check(denied.StatusCode == HttpStatusCode.Forbidden, "Only Admin may create museum: " + role);
    }
    using var created = await Send(HttpMethod.Post, "/api/museums/with-image", "Admin", png);
    Check(created.StatusCode == HttpStatusCode.Created, "Multipart create failed: " + await created.Content.ReadAsStringAsync());
    using var result = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    var id = result.RootElement.GetProperty("data").GetProperty("id").GetInt32();
    var url = result.RootElement.GetProperty("data").GetProperty("coverImageUrl").GetString()!;
    Check(await inspection.Museums.CountAsync() == 1, "One create must produce one museum");
    using var fetched = await client.GetAsync(url);
    Check(fetched.IsSuccessStatusCode && (await fetched.Content.ReadAsByteArrayAsync()).SequenceEqual(png), "Published image bytes must survive a GET");
    Check(fetched.Content.Headers.ContentType?.MediaType == "image/png" && fetched.Headers.Contains("X-Content-Type-Options"), "Image MIME and nosniff");
    var before = storage.Files.Count;
    using var duplicate = await Send(HttpMethod.Post, "/api/museums/with-image", "Admin", png);
    Check(duplicate.StatusCode == HttpStatusCode.BadRequest && await inspection.Museums.CountAsync() == 1 && storage.Files.Count == before, "Duplicate metadata must not upload an image");
    using var invalid = await Send(HttpMethod.Put, $"/api/museums/{id}/with-image", "Admin", png, filename: "bad.jpg");
    Check(invalid.StatusCode == HttpStatusCode.BadRequest && (await inspection.Museums.AsNoTracking().SingleAsync(m => m.Id == id)).CoverImageUrl == url, "Invalid replacement must preserve the old cover");
    using var staff = await Send(HttpMethod.Post, $"/api/museums/{id}/image", "Museum Staff", png);
    Check(staff.StatusCode == HttpStatusCode.Forbidden, "Unassigned/invalid staff identity must not change cover");
    using var updated = await Send(HttpMethod.Put, $"/api/museums/{id}/with-image", "Admin", png, "Bảo tàng đã cập nhật");
    var saved = await inspection.Museums.AsNoTracking().SingleAsync(m => m.Id == id);
    Check(updated.IsSuccessStatusCode && saved.Name == "Bảo tàng đã cập nhật" && saved.CoverImageUrl != url, "Update must save image and metadata together");
    Check(storage.Files.ContainsKey($"museums/{id}/images/{Path.GetFileName(new Uri(url).AbsolutePath)}"), "Old image is retained for potentially shared references");
    before = storage.Files.Count;
    failure.FailAt = failure.Calls + 2;
    using var failedSave = await Send(HttpMethod.Put, $"/api/museums/{id}/with-image", "Admin", png, "Lỗi lưu metadata");
    Check(failedSave.StatusCode == HttpStatusCode.InternalServerError && storage.Files.Count == before, "Second metadata failure must clean the new R2 object");
    failure.FailAt = 0;
    storage.Unavailable = true;
    using var unavailable = await Send(HttpMethod.Put, $"/api/museums/{id}/with-image", "Admin", png, "Kho chưa sẵn sàng");
    Check(unavailable.StatusCode == HttpStatusCode.ServiceUnavailable && !(await unavailable.Content.ReadAsStringAsync()).Contains("private provider details"), "Storage errors use the safe existing media filter");
    storage.Unavailable = false;
    using var removed = await Send(HttpMethod.Delete, $"/api/museums/{id}/image", "Admin", null);
    Check(removed.IsSuccessStatusCode && (await inspection.Museums.AsNoTracking().SingleAsync(m => m.Id == id)).CoverImageUrl == null, "Remove cover");
    try { R2FileStorageService.ValidateKey("../outside.png"); throw new Exception("Unsafe path accepted"); } catch (ArgumentException) { }
    Console.WriteLine("PASS: HTTP multipart create/update, actual service/repository, scoped R2 keys, image retrieval, role denials, failure cleanup, safe storage errors, shared-image retention and removal. EF InMemory/storage fixtures; no live PostgreSQL/R2 or transaction rollback proof.");
} finally {
    await app.StopAsync();
}

sealed class MemoryStorage : IFileStorageService {
    public Dictionary<string, byte[]> Files { get; } = [];
    public bool Unavailable;
    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) {
        R2FileStorageService.ValidateKey(key);
        if (Unavailable) throw new MediaStorageException();
        using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, cancellationToken); Files[key] = buffer.ToArray();
    }
    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream(Files[key]));
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { Files.Remove(key); return Task.CompletedTask; }
    public Task<StoredFileMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(Files.TryGetValue(key, out var bytes) ? new StoredFileMetadata(bytes.Length, "image/png") : null);
    public Task<string> GetDownloadUrlAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
sealed class FailSave : SaveChangesInterceptor {
    public int Calls, FailAt;
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        if (++Calls == FailAt) throw new DbUpdateException("Simulated metadata persistence failure");
        return base.SavingChangesAsync(data, result, cancellationToken);
    }
}
sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "invalid-staff-id"), new Claim(ClaimTypes.Role, role) };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}
