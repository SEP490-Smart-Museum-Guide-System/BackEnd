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
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Artifact;

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
        var assetService = DispatchProxy.Create<IArtifactAssetService, ScanAssetProxy>();
        var assetState = (ScanAssetProxy)(object)assetService;
        var controller = new ArtifactScanController(scope, assetService, http, config, NullLogger<ArtifactScanController>.Instance) {
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
        http.Json = "{\"artifactId\":7,\"status\":\"REVIEW_REQUIRED\"}";
        Check(await controller.Save(7, job, default) is ObjectResult { StatusCode: 200 }, "Scan: reviewed GLB saves through asset service");
        Check(assetState.Bytes.SequenceEqual(http.Model) && http.Approvals == 1, "Scan: original GLB bytes reach asset service before approval");
        assetState.Success = false;
        Check(await controller.Save(7, job, default) is ObjectResult { StatusCode: 503 }, "Scan: storage failure is visible");
        Check(http.Approvals == 1, "Scan: failed storage does not complete AI job");
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
    public byte[] Model = "glTF-test-model"u8.ToArray(); public int Approvals;
    public int Calls; public string? Key; public string Json = "{}"; public HttpStatusCode Status = HttpStatusCode.OK;
    public HttpClient CreateClient(string name) => new(this, false);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++; Key = request.Headers.GetValues("X-Service-Key").Single();
        if (request.RequestUri!.AbsolutePath.EndsWith("/approve")) Approvals++;
        HttpContent content = request.RequestUri.AbsolutePath.EndsWith("/model") ? new StreamContent(new ScanNetworkStream(Model)) : new StringContent(Json, System.Text.Encoding.UTF8, "application/json");
        if (request.RequestUri.AbsolutePath.EndsWith("/model")) content.Headers.ContentLength = Model.Length;
        return Task.FromResult(new HttpResponseMessage(Status) { Content = content });
    }
}
class ScanNetworkStream(byte[] bytes) : Stream
{
    private readonly MemoryStream inner = new(bytes);
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => inner.ReadAsync(buffer, token);
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
}
public class ScanAssetProxy : DispatchProxy
{
    public byte[] Bytes = []; public bool Success = true;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name != "UploadOrReplace3DModelAsync") throw new NotSupportedException();
        if (!((Stream)args![1]!).CanSeek) throw new Exception("GLB validation needs seekable stream");
        using var buffer = new MemoryStream(); ((Stream)args![1]!).CopyTo(buffer); Bytes = buffer.ToArray();
        return Task.FromResult(Success ? ResponseDto<Artifact3DModelResponseDto>.Success(new Artifact3DModelResponseDto())
            : ResponseDto<Artifact3DModelResponseDto>.Fail("storage unavailable", 503));
    }
}
