using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

static class VisionChecks
{
    public static async Task Run()
    {
        void Check(bool value, string name) { if (!value) throw new Exception("Vision: " + name); }
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var scope = DispatchProxy.Create<IAccountAdminService, ScanScopeProxy>();
        var state = (ScanScopeProxy)(object)scope;
        var http = new VisionHttp();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiVision:BaseUrl"] = "https://vision.test", ["AiVision:ServiceKey"] = "test-server-key"
        }).Build();
        var controller = new ArtifactVisionController(scope, db, http, config) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        IFormFile Photo() => new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "image", "photo.jpg");
        Check(await controller.Health(default) is UnauthorizedResult, "anonymous denied");
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, RoleNames.MuseumStaff)], "test"));
        Check(await controller.Index(7, Photo(), default) is ObjectResult { StatusCode: 403 }, "unassigned staff denied");
        Check(await controller.Delete(7, default) is ObjectResult { StatusCode: 403 } && http.Calls == 0, "denied mutations never reach AI");
        state.Allowed = true;
        Check(await controller.Index(7, Photo(), default) is OkObjectResult, "assigned staff can index");
        Check(http.Path == "/api/v1/references?artifact_id=7" && http.Key == "test-server-key", "BE supplies internal key and artifact ID");
        http.Status = HttpStatusCode.Unauthorized;
        Check(await controller.Health(default) is ObjectResult { StatusCode: 503 }, "internal key failure is configuration error");
        http.Status = HttpStatusCode.OK;
        var active = new Museum { Id = 1, IsActive = true };
        var inactive = new Museum { Id = 2, IsActive = false };
        db.Museums.AddRange(active, inactive);
        foreach (var id in new[] { 7, 8, 9, 10 }) {
            var artifact = new Artifact { Id = id, Museum = id == 10 ? inactive : active, IsActive = id != 9 };
            var content = new ArtifactContent { Id = id, Artifact = artifact, LanguageCode = "vi" };
            db.Set<ArtifactContentVersion>().Add(new ArtifactContentVersion {
                Id = id, Content = content, Status = id == 8 ? ContentStatuses.PendingReview : ContentStatuses.Published,
                IsCurrent = true, VersionNo = 1
            });
        }
        await db.SaveChangesAsync();
        http.Json = "{\"matched\":true,\"artifactId\":8,\"similarity\":0.99,\"candidates\":[{\"artifactId\":8,\"similarity\":0.99},{\"artifactId\":7,\"similarity\":0.8}]}";
        var result = (OkObjectResult)await controller.Recognize(Photo());
        var data = ((ResponseDto<JsonObject>)result.Value!).Data!;
        Check(http.ArtifactIds == "[7]", "only active published artifacts in active museums enter search scope");
        Check(data["matched"]!.GetValue<bool>() == false && data["artifactId"] == null, "older AI cannot return draft match");
        Check(data["candidates"]!.AsArray().Count == 1 && data["candidates"]![0]!["artifactId"]!.GetValue<int>() == 7, "private candidates filtered defensively");
        var calls = http.Calls;
        Check(await controller.Recognize(Photo(), museumId: 999) is OkObjectResult && http.Calls == calls, "empty museum never calls global AI index");
        Check(await controller.Recognize(Photo(), topK: 11) is ObjectResult { StatusCode: 422 }, "invalid top K denied");
        Check(typeof(ArtifactVisionController).GetCustomAttribute<AuthorizeAttribute>() != null, "all routes require authentication");
        foreach (var name in new[] { "Index", "Delete" })
            Check(typeof(ArtifactVisionController).GetMethod(name)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles == RoleNames.Admin + "," + RoleNames.MuseumStaff, "reference mutations restricted to staff");
        Console.WriteLine("PASS: Vision JWT gateway, staff assignment, internal key handling, published catalog scope and private result filtering.");
    }
}

class VisionHttp : HttpMessageHandler, IHttpClientFactory
{
    public int Calls; public string? Key, Path, ArtifactIds;
    public HttpStatusCode Status = HttpStatusCode.OK; public string Json = "{}";
    public HttpClient CreateClient(string name) => new(this, false);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++; Key = request.Headers.GetValues("X-Service-Key").Single(); Path = request.RequestUri!.PathAndQuery;
        if (request.Content is MultipartFormDataContent form)
            foreach (var part in form)
                if (part.Headers.ContentDisposition?.Name?.Trim('"') == "artifact_ids") ArtifactIds = await part.ReadAsStringAsync(ct);
        return new HttpResponseMessage(Status) { Content = new StringContent(Json, System.Text.Encoding.UTF8, "application/json") };
    }
}
