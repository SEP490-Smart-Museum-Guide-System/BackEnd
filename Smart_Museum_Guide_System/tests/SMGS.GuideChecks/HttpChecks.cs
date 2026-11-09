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
        builder.Services.AddSingleton<IStaffAiService, StudioFixture>();
        var speech = new SpeechFixture();
        builder.Services.AddSingleton<IArtifactSpeech>(speech);
        builder.Services.AddSingleton<IStaffAiSource, SpeechSourceFixture>();
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
            const string studio = "/api/artifacts/17/ai/drafts";
            foreach (var role in new[] { RoleNames.Visitor, RoleNames.Curator, RoleNames.Staff, RoleNames.Admin, RoleNames.MuseumStaff })
            {
                http.DefaultRequestHeaders.Remove("X-Test-Role"); http.DefaultRequestHeaders.Add("X-Test-Role", role);
                var status = (await http.PostAsJsonAsync(studio, new { mode = "quiz", sourceText = "Tư liệu kiểm thử từ bảo tàng." })).StatusCode;
                if (status != (role is RoleNames.Admin or RoleNames.MuseumStaff ? HttpStatusCode.OK : HttpStatusCode.Forbidden)) throw new Exception("Studio role boundary failed.");
            }
            if ((await http.PostAsJsonAsync(studio, new { mode = "multilingual", sourceText = "Tư liệu" })).StatusCode != HttpStatusCode.BadRequest) throw new Exception("Unsupported generation mode accepted.");
            const string preview = "/api/artifacts/17/ai/speech-preview";
            http.DefaultRequestHeaders.Remove("X-Test-Role");
            if ((await http.PostAsJsonAsync(preview, new { text = "Bản đọc" })).StatusCode != HttpStatusCode.Unauthorized) throw new Exception("Anonymous preview accepted.");
            foreach (var role in new[] { RoleNames.Visitor, RoleNames.Curator, RoleNames.Staff, RoleNames.Admin, RoleNames.MuseumStaff }) {
                http.DefaultRequestHeaders.Remove("X-Test-Role"); http.DefaultRequestHeaders.Add("X-Test-Role", role);
                var response = await http.PostAsJsonAsync(preview, new { text = " Bản đọc đã hiệu đính. " });
                if (response.StatusCode != (role is RoleNames.Admin or RoleNames.MuseumStaff ? HttpStatusCode.OK : HttpStatusCode.Forbidden)) throw new Exception("Speech role boundary failed.");
                if (response.IsSuccessStatusCode && (response.Content.Headers.ContentType?.MediaType != "audio/wav" || speech.Text != "Bản đọc đã hiệu đính.")) throw new Exception("Edited narration did not reach speech provider.");
            }
            var calls = speech.Calls;
            if ((await http.PostAsJsonAsync("/api/artifacts/18/ai/speech-preview", new { text = "Bản đọc" })).StatusCode != HttpStatusCode.Forbidden || speech.Calls != calls) throw new Exception("Cross-museum speech accepted.");
            if ((await http.PostAsJsonAsync(preview, new { text = " " })).StatusCode != HttpStatusCode.BadRequest || speech.Calls != calls) throw new Exception("Empty preview reached model.");
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
class StudioFixture : IStaffAiService {
    public Task<ResponseDto<StaffAiResult>> GenerateAsync(int artifactId, Guid user, bool admin, StaffAiRequest request, CancellationToken token = default)
        => Task.FromResult(ResponseDto<StaffAiResult>.Fail("Fixture không ghi dữ liệu.", 200));
}
class SpeechSourceFixture : IStaffAiSource {
    public Task<StaffAiSource> GetAsync(int artifactId, Guid user, bool admin, CancellationToken cancellationToken = default) {
        if (artifactId != 17) throw new SMGS.Application.DTOs.Experience.ExperienceException("Ngoài phạm vi.", 403);
        return Task.FromResult(new StaffAiSource(17, "Hiện vật", null, null, ""));
    }
}
class SpeechFixture : IArtifactSpeech {
    public string? Text; public int Calls;
    public Task<SpeechAudio> GenerateAsync(string text, CancellationToken cancellationToken = default) {
        Calls++; Text = text;
        var bytes = new byte[46]; "RIFF"u8.CopyTo(bytes); "WAVE"u8.CopyTo(bytes.AsSpan(8));
        return Task.FromResult(new SpeechAudio(bytes));
    }
}
