using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.ArtifactContent.Request;
using SMGS.Application.Services;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
var curator = typeof(ArtifactContentsController).GetCustomAttribute<AuthorizeAttribute>();
Check(curator?.Roles == RoleNames.Curator, "Review endpoints must require Curator.");
Check(!typeof(ArtifactContentsController).GetMethods().Any(m => m.IsDefined(typeof(AllowAnonymousAttribute))), "Review actions must not bypass authorization.");
var submit = typeof(ArtifactContentSubmissionsController).GetMethod("Submit")!;
Check(submit.GetCustomAttribute<AuthorizeAttribute>()?.Roles == $"{RoleNames.Admin},{RoleNames.MuseumStaff}", "Only staff/admin submit AI output.");
Check(typeof(ArtifactContentSubmissionsController).GetMethod("GetPublished")!.IsDefined(typeof(AuthorizeAttribute)), "Visitor content requires login.");
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
Check((await service.ReviewAsync(1, new(), user, true)).StatusCode == 409, "Already reviewed versions cannot be approved again.");
await service.SubmitAsync(1, dto, user);
var rejected = await service.ReviewAsync(2, new() { Note = " Incorrect date " }, user, false);
Check(rejected.Data is { Status: ContentStatuses.Rejected, IsCurrent: false, ReviewNote: "Incorrect date", PublishedAt: null }, "Rejection must never publish.");
var paged = await service.GetAllAsync(null, null, null, -1, 1000);
Check(paged.Data is { Page: 1, PageSize: 100 }, "Pagination is bounded.");
var invalid = new SubmitArtifactContentRequestDto { LanguageCode = "../vi", Title = "x", Description = "x" };
Check(!Validator.TryValidateObject(invalid, new ValidationContext(invalid), new List<ValidationResult>(), true), "Language format must be validated.");
if (args.Contains("--database")) await DatabaseChecks.RunAsync();
Console.WriteLine("PASS: authorization, submission validation, review guards, audit mapping, conflicts and pagination.");

sealed class ContentRepository : IArtifactContentRepository
{
    readonly List<ArtifactContentVersion> versions = new();
    public bool Conflict { get; set; }
    public int Reviews { get; private set; }
    public Task<ArtifactContentVersion?> GetByIdAsync(int id) => Task.FromResult(versions.FirstOrDefault(v => v.Id == id));
    public Task<ArtifactContentVersion?> GetPublishedAsync(int id, string language) => Task.FromResult(versions.FirstOrDefault(v => v.IsCurrent));
    public Task<(List<ArtifactContentVersion> Items, int TotalCount)> GetPagedAsync(int? museum, int? artifact, string? status, int page, int size) => Task.FromResult((versions.ToList(), versions.Count));
    public Task<bool> CreateAsync(int id, string language, ArtifactContentVersion version)
    {
        version.Id = versions.Count + 1;
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
