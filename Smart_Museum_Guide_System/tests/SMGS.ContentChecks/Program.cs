using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.ArtifactContent.Request;
using SMGS.Application.Services;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var curator = typeof(ArtifactContentsController).GetCustomAttribute<AuthorizeAttribute>();
Check(curator?.Roles == RoleNames.Curator, "Review endpoints must require Curator.");
Check(!typeof(ArtifactContentsController).GetMethods().Any(m => m.IsDefined(typeof(AllowAnonymousAttribute))), "Review actions must not bypass authorization.");
var submit = typeof(ArtifactContentSubmissionsController).GetMethod("Submit")!;
Check(submit.GetCustomAttribute<AuthorizeAttribute>()?.Roles == $"{RoleNames.Admin},{RoleNames.MuseumStaff}", "Only staff/admin submit AI output.");
var submitManual = typeof(ArtifactContentSubmissionsController).GetMethod("SubmitManual")!;
Check(submitManual.GetCustomAttribute<AuthorizeAttribute>()?.Roles == $"{RoleNames.Admin},{RoleNames.MuseumStaff}", "Only staff/admin submit manual narration.");
Check(typeof(ArtifactContentSubmissionsController).GetMethod("GetPublished")!.IsDefined(typeof(AllowAnonymousAttribute)), "Free published content is available anonymously; service enforces premium access.");

// CD-01 controller policy checks
var adminAttr = typeof(AccountsController).GetCustomAttribute<AuthorizeAttribute>();
Check(adminAttr?.Roles == RoleNames.Admin, "AccountsController must require Admin role.");
Check(typeof(AccountsController).GetMethod("CreateStaff") is not null
    && typeof(AccountsController).GetMethod("UpdateAccount") is not null
    && typeof(AccountsController).GetMethod("SetStatus") is not null
    && typeof(AccountsController).GetMethod("AssignRoles") is not null
    && typeof(AccountsController).GetMethod("AssignScope") is not null
    && typeof(AccountsController).GetMethod("RevokeScope") is not null,
    "CD-01 endpoints must exist on AccountsController.");

// CD-02 structure controller endpoints checks
Check(typeof(IndoorMapsController).GetMethod("UpdateBuilding") is not null
    && typeof(IndoorMapsController).GetMethod("DeleteBuilding") is not null
    && typeof(IndoorMapsController).GetMethod("UpdateFloor") is not null
    && typeof(IndoorMapsController).GetMethod("DeleteFloor") is not null
    && typeof(IndoorMapsController).GetMethod("AddGallery") is not null
    && typeof(IndoorMapsController).GetMethod("UpdateGallery") is not null
    && typeof(IndoorMapsController).GetMethod("DeleteGallery") is not null,
    "CD-02 building/floor/gallery CRUD endpoints must exist on IndoorMapsController.");

// CD-03 topics controller checks
Check(typeof(TopicsController).GetMethod("Create") is not null
    && typeof(TopicsController).GetMethod("Update") is not null
    && typeof(TopicsController).GetMethod("Delete") is not null
    && typeof(TopicsController).GetMethod("AssignTopics") is not null
    && typeof(TopicsController).GetMethod("RemoveTopic") is not null,
    "CD-03 topic endpoints must exist on TopicsController.");

// CD-06/07 media & 3D controller checks
Check(typeof(ArtifactAssetsController).GetMethod("ListMedia") is not null
    && typeof(ArtifactAssetsController).GetMethod("UploadMedia") is not null
    && typeof(ArtifactAssetsController).GetMethod("DeleteMedia") is not null
    && typeof(ArtifactAssetsController).GetMethod("UploadOrReplace3DModel") is not null
    && typeof(ArtifactAssetsController).GetMethod("Delete3DModel") is not null
    && typeof(ArtifactAssetsController).GetMethod("GetViewerData") is not null,
    "CD-06B & CD-07 media/3D/viewer endpoints must exist on ArtifactAssetsController.");

// CD-06A manual narration + versioning + review workflow checks
var repository = new ContentRepository();
var artifacts = new Artifacts();
var service = new ArtifactContentService(repository, artifacts);
var user = Guid.NewGuid();
Check((await service.GetByIdAsync(99)).StatusCode == 404, "Missing version should be 404.");
Check((await service.ReviewAsync(99, new(), user, true)).StatusCode == 404, "Missing review should be 404.");
Check((await service.GetAllAsync(null, null, "invalid", 1, 10)).StatusCode == 400, "Unknown status should be rejected.");
Check((await service.SubmitAsync(1, new(), user)).StatusCode == 400, "Blank submission must fail.");
var dto = new SubmitArtifactContentRequestDto { LanguageCode = "VI", Title = " Title ", Description = " Content " };
Check((await service.SubmitAsync(99, dto, user)).StatusCode == 404, "Missing artifact should be 404.");
artifacts.Item.IsActive = false;
Check((await service.SubmitAsync(1, dto, user)).StatusCode == 409, "Inactive artifact cannot receive submissions.");
artifacts.Item.IsActive = true;
Check((await service.SubmitAsync(1, dto, Guid.Empty)).StatusCode == 401, "Missing caller must fail.");
var submitted = await service.SubmitAsync(1, dto, user);
Check(submitted.StatusCode == 201 && submitted.Data is { Title: "Title", Description: "Content", LanguageCode: "vi", IsCurrent: false, IsAiGenerated: true, Status: ContentStatuses.PendingReview }, "Submission must be normalized and pending, never public.");
Check(submitted.Data!.CreatedBy == user, "Creator comes from authenticated caller.");
Check((await service.ReviewAsync(1, new() { Note = "  " }, user, false)).StatusCode == 400, "Rejection requires a reason.");
Check(repository.Reviews == 0, "Invalid reviews must not write.");
repository.Conflict = true;
Check((await service.ReviewAsync(1, new(), user, true)).StatusCode == 409, "Concurrent review should return 409.");
repository.Conflict = false;
var approved = await service.ReviewAsync(1, new() { Note = " Checked " }, user, true);
Check(approved.Data is { Status: ContentStatuses.Published, IsCurrent: true, ReviewNote: "Checked" } && approved.Data.ReviewedBy == user, "Approval records reviewer and publication.");
Check((await service.GetPublishedAsync(1, "vi")).StatusCode == 200, "Free content remains available.");
artifacts.Item.AccessLevel = "premium";
Check((await service.GetPublishedAsync(1, "vi")).StatusCode == 403, "Premium content must fail closed without an entitlement provider.");
artifacts.Item.AccessLevel = "free";
Check((await service.ReviewAsync(1, new(), user, true)).StatusCode == 409, "Already reviewed versions cannot be approved again.");
await service.SubmitAsync(1, dto, user);
var rejected = await service.ReviewAsync(2, new() { Note = " Incorrect date " }, user, false);
Check(rejected.Data is { Status: ContentStatuses.Rejected, IsCurrent: false, ReviewNote: "Incorrect date", PublishedAt: null }, "Rejection must never publish.");

// Manual narration submission & approval check
var manualDto = new SubmitArtifactContentRequestDto
{
    LanguageCode = "en",
    Title = " Bronze Drum ",
    Description = " Dong Son era ",
    NarrationText = " Welcome to the Bronze Drum exhibit. ",
    AudioUrl = " /api/artifacts/1/media/5/file ",
    IsAiGenerated = false
};
var manualSubmitted = await service.SubmitAsync(1, manualDto, user);
Check(manualSubmitted.StatusCode == 201 && manualSubmitted.Data is
{
    LanguageCode: "en",
    Title: "Bronze Drum",
    NarrationText: "Welcome to the Bronze Drum exhibit.",
    AudioUrl: "/api/artifacts/1/media/5/file",
    IsAiGenerated: false,
    Status: ContentStatuses.PendingReview
}, "Manual narration must store NarrationText, AudioUrl and IsAiGenerated=false.");
var manualApproved = await service.ReviewAsync(3, new() { Note = "Approved manual narration" }, user, true);
Check(manualApproved.Data is { Status: ContentStatuses.Published, IsCurrent: true, NarrationText: "Welcome to the Bronze Drum exhibit." }, "Manual narration reuses Curator review workflow.");
var history = await service.GetVersionHistoryAsync(1, "en");
Check(history.StatusCode == 200 && history.Data?.Count == 1, "Version history filters by language.");

var paged = await service.GetAllAsync(null, null, null, -1, 1000);
Check(paged.Data is { Page: 1, PageSize: 100 }, "Pagination is bounded.");
var invalid = new SubmitArtifactContentRequestDto { LanguageCode = "../vi", Title = "x", Description = "x" };
Check(!Validator.TryValidateObject(invalid, new ValidationContext(invalid), new List<ValidationResult>(), true), "Language format must be validated.");

// CD-06B & CD-07 Media and 3D model validation checks
var mediaCheck = ArtifactAssetStore.ValidateMedia([1, 2, 3, 4], "narration.mp3", "audio");
Check(mediaCheck is { MediaType: "audio", ContentType: "audio/mpeg", Extension: ".mp3" }, "Valid audio media accepted.");
try { ArtifactAssetStore.ValidateMedia([1, 2], "script.exe"); throw new Exception("Exe must fail"); } catch (ArgumentException) { }
try { ArtifactAssetStore.ValidateMedia([1, 2], "photo.png", "video"); throw new Exception("Mismatched mediaType must fail"); } catch (ArgumentException) { }

var glbBytes = Convert.FromBase64String("Z2xURgIAAAAwAAAAHAAAAEpTT057ImFzc2V0Ijp7InZlcnNpb24iOiIyLjAifX0g");
var glbCheck = ArtifactAssetStore.Validate3DModel(glbBytes, "drum.glb");
Check(glbCheck is { Format: "glb", ContentType: "model/gltf-binary", Extension: ".glb" }, "Valid GLB header accepted.");
var gltfBytes = Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"}}");
try { ArtifactAssetStore.Validate3DModel(gltfBytes, "drum.gltf"); throw new Exception("GLTF must fail"); } catch (ArgumentException) { }
try { ArtifactAssetStore.Validate3DModel([1, 2, 3, 4], "fake.glb"); throw new Exception("Fake GLB must fail"); } catch (ArgumentException) { }
try { ArtifactAssetStore.Validate3DModel(Encoding.UTF8.GetBytes("{}"), "invalid.gltf"); throw new Exception("GLTF without asset must fail"); } catch (ArgumentException) { }

if (args.Contains("--database")) await DatabaseChecks.RunAsync();
Console.WriteLine("PASS: authorization, CD-01/CD-02/CD-03/CD-06/CD-07 endpoints, manual narration & versioning, media/3D validation, review guards, audit mapping, conflicts and pagination.");

sealed class ContentRepository : IArtifactContentRepository
{
    readonly List<ArtifactContentVersion> versions = new();
    public bool Conflict { get; set; }
    public int Reviews { get; private set; }
    public Task<ArtifactContentVersion?> GetByIdAsync(int id) => Task.FromResult(versions.FirstOrDefault(v => v.Id == id));
    public Task<ArtifactContentVersion?> GetPublishedAsync(int id, string language) => Task.FromResult(versions.FirstOrDefault(v => v.IsCurrent && v.Content.LanguageCode == language));
    public Task<(List<ArtifactContentVersion> Items, int TotalCount)> GetPagedAsync(int? museum, int? artifact, string? status, int page, int size) => Task.FromResult((versions.ToList(), versions.Count));
    public Task<bool> CreateAsync(int id, string language, ArtifactContentVersion version)
    {
        version.Id = versions.Count + 1;
        version.VersionNo = versions.Where(v => v.Content.LanguageCode == language).Select(v => v.VersionNo).DefaultIfEmpty(0).Max() + 1;
        version.Content = new() { ArtifactId = id, LanguageCode = language, Artifact = new() { MuseumId = 1, Name = "Artifact" } };
        versions.Add(version);
        return Task.FromResult(true);
    }
    public Task<bool> ReviewAsync(ArtifactContentVersion v, Guid reviewer, bool approve, string? note)
    {
        Reviews++;
        if (Conflict) return Task.FromResult(false);
        v.Status = approve ? ContentStatuses.Published : ContentStatuses.Rejected;
        v.IsCurrent = approve;
        v.ReviewedBy = reviewer; v.ReviewedAt = DateTime.UtcNow; v.ReviewNote = note;
        v.PublishedAt = approve ? v.ReviewedAt : null;
        return Task.FromResult(true);
    }
}
sealed class Artifacts : IArtifactRepository
{
    public Artifact Item { get; } = new() { Id = 1, MuseumId = 1, Name = "Artifact" };
    public Task<Artifact?> GetByIdAsync(int id) => Task.FromResult(id == 1 ? Item : null);
    public Task<(IList<Artifact> Items, int TotalCount)> GetPagedAsync(int? museum, int? gallery, string? keyword, bool? active, int page, int size) => throw new NotSupportedException();
    public Task<bool> ExistsByQrCodeAsync(string qr, int? excludeId = null) => throw new NotSupportedException();
    public Task<bool> ExistsByCodeAsync(int museum, string code, int? excludeId = null) => throw new NotSupportedException();
    public Task<Artifact> CreateAsync(Artifact artifact) => throw new NotSupportedException();
    public Task UpdateAsync(Artifact artifact) => throw new NotSupportedException();
}
