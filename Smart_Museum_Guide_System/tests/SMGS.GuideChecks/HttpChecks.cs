using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

internal static class HttpChecks {
    public static async Task RunAsync() {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(ArtifactGuideController).Assembly);
        builder.Services.AddSingleton<IArtifactGuideService, GuideFixture>();
        // Isolated authentication fixture; existing auth suites verify actual JWT issuance/validation.
        builder.Services.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuth>("fixture", _ => {});
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("artifact-guide", x => {
            x.PermitLimit = 30; x.Window = TimeSpan.FromMinutes(1); x.QueueLimit = 0;
        }));
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http = new HttpClient { BaseAddress = new Uri(address) };
            const string path = "/api/artifacts/17/guide/messages";
            async Task CheckStatus(HttpStatusCode expected, object body) {
                if ((await http.PostAsJsonAsync(path, body)).StatusCode != expected) throw new Exception($"Guide HTTP expected {expected}.");
            }
            await CheckStatus(HttpStatusCode.Unauthorized, new { question = "Hỏi" });
            http.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.Admin);
            await CheckStatus(HttpStatusCode.Forbidden, new { question = "Hỏi" });
            http.DefaultRequestHeaders.Remove("X-Test-Role"); http.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.Visitor);
            await CheckStatus(HttpStatusCode.BadRequest, new { question = new string('x', 1001) });
            await CheckStatus(HttpStatusCode.BadRequest, new { question = "Hỏi", history = new[] { new { role = "system", text = "A" } } });
            await CheckStatus(HttpStatusCode.OK, new { question = "Hỏi", history = Array.Empty<object>() });
        } finally { await app.StopAsync(); }
    }
}
class FixtureAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "44444444-4444-4444-4444-444444444444"), new Claim(ClaimTypes.Role, role) }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
class GuideFixture : IArtifactGuideService {
    public Task<ResponseDto<GuideAnswer>> AskAsync(int artifactId, Guid userId, GuideMessageRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseDto<GuideAnswer>.Success(new(artifactId, "Nội dung kiểm thử.", false, [])));
}
