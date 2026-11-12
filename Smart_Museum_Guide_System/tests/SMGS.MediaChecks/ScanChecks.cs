using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SMGS.API.Controllers;
using SMGS.Application.Interfaces;

static class ScanChecks
{
    public static async Task Run()
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        var scope = DispatchProxy.Create<IAccountAdminService, ScanScopeProxy>();
        var state = (ScanScopeProxy)(object)scope;
        var http = new ScanHttp();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AiScan:BaseUrl"] = "http://scan.test", ["AiScan:ServiceKey"] = "internal-test-key"
        }).Build();
        var controller = new ArtifactScanController(scope, null!, http, config, NullLogger<ArtifactScanController>.Instance) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var job = Guid.NewGuid();
        Check(await controller.Status(7, job, default) is UnauthorizedResult, "Scan: anonymous rejected");
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Museum Staff")], "test"));
        Check(await controller.Status(7, job, default) is ObjectResult { StatusCode: 403 }, "Scan: staff must be assigned");
        Check(http.Calls == 0, "Scan: forbidden request never reaches AI");
        state.Allowed = true;
        http.Json = "{\"artifactId\":8,\"status\":\"REVIEW_REQUIRED\"}";
        Check(await controller.Save(7, job, default) is ObjectResult { StatusCode: 403 }, "Scan: cross-artifact save denied before storage");
        http.Json = "{\"artifactId\":7,\"status\":\"PROCESSING\"}";
        Check(await controller.Save(7, job, default) is ObjectResult { StatusCode: 409 }, "Scan: incomplete result cannot be published");
        http.Status = HttpStatusCode.Unauthorized;
        Check(await controller.List(7, default) is ObjectResult { StatusCode: 503 }, "Scan: internal key mismatch is configuration failure");
        var roles = typeof(ArtifactScanController).GetCustomAttribute<AuthorizeAttribute>()!.Roles!;
        Check(roles.Contains("Museum Staff") && !roles.Contains("Visitor"), "Scan: staff/admin authorization");
        Check(http.Key == "internal-test-key", "Scan: BE supplies server key");
        Console.WriteLine("PASS: scan gateway staff scope, cross-artifact save rejection, unfinished job rejection, internal auth failure.");
    }
}
public class ScanScopeProxy : DispatchProxy
{
    public bool Allowed;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "CanManageArtifactAsync"
        ? Task.FromResult(Allowed) : throw new NotSupportedException();
}
class ScanHttp : HttpMessageHandler, IHttpClientFactory
{
    public int Calls; public string? Key; public string Json = "{}"; public HttpStatusCode Status = HttpStatusCode.OK;
    public HttpClient CreateClient(string name) => new(this, false);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++; Key = request.Headers.GetValues("X-Service-Key").Single();
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Json, System.Text.Encoding.UTF8, "application/json") });
    }
}
