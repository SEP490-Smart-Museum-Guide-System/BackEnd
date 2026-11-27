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
await GuideAndDashboardDbChecks.RunAsync();
var database = "museum-forms-" + Guid.NewGuid().ToString("N");
var storage = new MemoryStorage();
var failure = new FailSave();
void ConfigureDb(DbContextOptionsBuilder options) => options.UseInMemoryDatabase(database)
    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).AddInterceptors(failure);
var inspectionOptions = new DbContextOptionsBuilder<ApplicationDbContext>(); ConfigureDb(inspectionOptions);
using var inspection = new ApplicationDbContext(inspectionOptions.Options);
inspection.Set<MuseumCategory>().AddRange(
    new MuseumCategory { Id = 1, Code = "history", Name = "Lịch sử", DisplayOrder = 1 },
    new MuseumCategory { Id = 2, Code = "culture", Name = "Văn hóa", DisplayOrder = 2 },
    new MuseumCategory { Id = 3, Code = "inactive", Name = "Ẩn", DisplayOrder = 3, IsActive = false });
await inspection.SaveChangesAsync();
failure.Calls = 0;
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
    async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? role, byte[]? image, string name = "Bảo tàng ảnh", string filename = "museum.png", int[]? categoryIds = null) {
        using var request = new HttpRequestMessage(method, path);
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        if (method != HttpMethod.Delete) {
            var body = new MultipartFormDataContent();
            body.Add(new StringContent(name), "name"); body.Add(new StringContent("79"), "cityCode");
            body.Add(new StringContent("Địa chỉ bảo tàng"), "address");
            foreach (var categoryId in categoryIds ?? [1]) body.Add(new StringContent(categoryId.ToString()), "categoryIds");
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
    using var invalidCategories = await Send(HttpMethod.Post, "/api/museums/with-image", "Admin", png, categoryIds: [1, 1]);
    Check(invalidCategories.StatusCode == HttpStatusCode.BadRequest, "Duplicate categories must be rejected");
    using var inactiveCategory = await Send(HttpMethod.Post, "/api/museums/with-image", "Admin", png, categoryIds: [3]);
    Check(inactiveCategory.StatusCode == HttpStatusCode.BadRequest, "Inactive categories must be rejected");
    using var unknownCategory = await Send(HttpMethod.Post, "/api/museums/with-image", "Admin", png, categoryIds: [99]);
    Check(unknownCategory.StatusCode == HttpStatusCode.BadRequest, "Unknown categories must be rejected");
    using var missingCategory = await Send(HttpMethod.Post, "/api/museums/with-image", "Admin", png, categoryIds: []);
    Check(missingCategory.StatusCode == HttpStatusCode.BadRequest, "New museums require a category");
    using var created = await Send(HttpMethod.Post, "/api/museums/with-image", "Admin", png, categoryIds: [1, 2]);
    Check(created.StatusCode == HttpStatusCode.Created, "Multipart create failed: " + await created.Content.ReadAsStringAsync());
    using var result = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
    var id = result.RootElement.GetProperty("data").GetProperty("id").GetInt32();
    var createdCategories = result.RootElement.GetProperty("data").GetProperty("categories");
    Check(createdCategories.GetArrayLength() == 2, "Museum DTO should contain both categories");
    foreach (var categoryId in new[] { 1, 2 })
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/museums?categoryId={categoryId}&sortBy=name&pageSize=1");
        Check(page.GetProperty("data").GetProperty("totalCount").GetInt32() == 1,
            "Each assigned category returns the museum once");
    }
    var categories = await client.GetFromJsonAsync<JsonElement>("/api/museum-categories");
    Check(categories.GetProperty("data").GetArrayLength() == 2, "Public category listing excludes inactive rows");
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
    async Task<HttpResponseMessage> ChangeCategory(int categoryId, string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/museum-categories/{categoryId}");
        request.Headers.Add("X-Test-Role", role);
        request.Content = JsonContent.Create(new { name = "Danh mục", displayOrder = 4, isActive = false });
        return await client.SendAsync(request);
    }
    using var staffCategoryChange = await ChangeCategory(2, "Museum Staff");
    Check(staffCategoryChange.StatusCode == HttpStatusCode.Forbidden, "Only admins can manage categories");
    using var protectLastCategory = await ChangeCategory(1, "Admin");
    Check(protectLastCategory.StatusCode == HttpStatusCode.BadRequest, "An active museum must retain one active category");
    using var deactivateUnusedCategory = await ChangeCategory(2, "Admin");
    Check(deactivateUnusedCategory.IsSuccessStatusCode, "An unused category can be deactivated");
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
    inspection.Set<VisitorHome>().Add(new VisitorHome { Id = 1 });
    await inspection.SaveChangesAsync();
    const string homePath = "/api/visitor-home";
    async Task<HttpResponseMessage> SaveHome(string role, Guid revision, object[] slides, params byte[][] images)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, homePath);
        request.Headers.Add("X-Test-Role", role);
        using var form = new MultipartFormDataContent();
        form.Add(JsonContent.Create(new { revision, title = "Bảo tàng", slides }), "draft");
        foreach (var image in images)
            form.Add(new ByteArrayContent(image), "files", "cover.png");
        request.Content = form;
        return await client.SendAsync(request);
    }

    using var emptyHome = await client.GetAsync(homePath);
    Check(emptyHome.IsSuccessStatusCode, "Visitor can read initial configuration");
    object[] initialSlides =
    [
        new { fileIndex = 0, title = "Kiến trúc", caption = "Mặt tiền" },
        new { fileIndex = 1, title = "Không gian", caption = "Sảnh chính" }
    ];
    using var forbiddenHome = await SaveHome("Visitor", Guid.Empty, initialSlides, png, png);
    Check(forbiddenHome.StatusCode == HttpStatusCode.Forbidden, "Visitor cannot save homepage");
    using var invalidHome = await SaveHome("Admin", Guid.Empty, initialSlides, png, [0]);
    Check(invalidHome.StatusCode == HttpStatusCode.BadRequest &&
        !storage.Files.Keys.Any(key => key.StartsWith("visitor-home/")), "Invalid batch must not publish either image");
    using var publishedHome = await SaveHome("Admin", Guid.Empty, initialSlides, png, png);
    Check(publishedHome.IsSuccessStatusCode, "Admin can publish a batch: " + await publishedHome.Content.ReadAsStringAsync());
    using var homeJson = JsonDocument.Parse(await publishedHome.Content.ReadAsStringAsync());
    var homeData = homeJson.RootElement.GetProperty("data");
    var homeSlides = homeData.GetProperty("slides");
    Check(homeSlides[0].GetProperty("title").GetString() == "Kiến trúc" &&
        homeSlides[1].GetProperty("title").GetString() == "Không gian", "Each image has its own title");
    var firstName = homeSlides[0].GetProperty("name").GetString();
    var secondName = homeSlides[1].GetProperty("name").GetString();
    using var firstImage = await client.GetAsync($"{homePath}/images/{firstName}");
    Check(firstImage.IsSuccessStatusCode && (await firstImage.Content.ReadAsByteArrayAsync()).SequenceEqual(png),
        "Visitor can load published image");
    Check(firstImage.Headers.CacheControl?.MaxAge == TimeSpan.FromHours(1), "Published image can be reused by the browser");
    using var staleHome = await SaveHome("Admin", Guid.Empty, []);
    Check(staleHome.StatusCode == HttpStatusCode.Conflict, "Outdated revisions cannot overwrite the published images");
    using var updatedHome = await SaveHome("Admin", homeData.GetProperty("revision").GetGuid(),
        [new { name = secondName, caption = "Sảnh" }]);
    Check(updatedHome.IsSuccessStatusCode, "Admin can keep an existing image and update its caption");
    var finalHome = await client.GetFromJsonAsync<JsonElement>(homePath);
    var finalSlides = finalHome.GetProperty("data").GetProperty("slides");
    Check(finalSlides.GetArrayLength() == 1 && finalSlides[0].GetProperty("name").GetString() == secondName &&
        finalSlides[0].GetProperty("caption").GetString() == "Sảnh", "Visitor reads the updated carousel");
    using var removedImage = await client.GetAsync($"{homePath}/images/{firstName}");
    Check(removedImage.StatusCode == HttpStatusCode.NotFound, "Removed image is no longer public");

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
