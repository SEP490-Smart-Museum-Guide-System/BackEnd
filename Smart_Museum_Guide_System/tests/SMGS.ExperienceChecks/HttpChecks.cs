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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Experience;
using SMGS.Domain.Constants;
using SMGS.Infrastructure;

static class HttpChecks
{
    public static async Task Run(string connection, TimeProvider clock, Guid visitor, Guid staff, Guid curator, int artifactId, int museumId, int tourId, long entryId)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = connection, ["JwtSettings:Key"] = new string('t', 64),
            ["JwtSettings:Issuer"] = "experience-test", ["JwtSettings:Audience"] = "experience-test",
            ["Osrm:BaseUrl"] = "http://127.0.0.1/", ["VisitorExperience:ScanPoints"] = "3", ["VisitorExperience:CorrectAnswerPoints"] = "5"
        });
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddSingleton(clock);
        builder.Services.AddControllers().AddApplicationPart(typeof(TourController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            async Task<HttpResponseMessage> Send(HttpMethod method, string url, Guid? user = null, string? role = null, object? body = null) {
                using var request = new HttpRequestMessage(method, url);
                if (user != null) { request.Headers.Add("X-Test-User", user.ToString()); request.Headers.Add("X-Test-Role", role); }
                if (body != null) request.Content = JsonContent.Create(body);
                return await client.SendAsync(request);
            }
            async Task Expect(HttpMethod method, string url, HttpStatusCode status, Guid? user = null, string? role = null, object? body = null) {
                using var response = await Send(method, url, user, role, body);
                if (response.StatusCode != status) throw new Exception($"HTTP {url}: expected {status}, got {response.StatusCode}");
            }
            await Expect(HttpMethod.Get, "/api/daily-quizzes/today", HttpStatusCode.Unauthorized);
            await Expect(HttpMethod.Get, "/api/daily-quizzes/today", HttpStatusCode.Forbidden, staff, RoleNames.MuseumStaff);
            await Expect(HttpMethod.Get, $"/api/tours/{tourId}", HttpStatusCode.OK);
            await Expect(HttpMethod.Get, $"/api/tours/manage?museumId={museumId}", HttpStatusCode.Forbidden, visitor, RoleNames.Visitor);
            await Expect(HttpMethod.Post, "/api/question-bank/questions/1/review", HttpStatusCode.Forbidden, staff, RoleNames.MuseumStaff, new ReviewQuestionRequest(true, null));
            await Expect(HttpMethod.Get, $"/api/question-bank/questions?artifactId={artifactId}", HttpStatusCode.OK, curator, RoleNames.Curator);
            await Expect(HttpMethod.Post, "/api/recognitions/qr", HttpStatusCode.BadRequest, visitor, RoleNames.Visitor, new { qrCode = "", requestId = Guid.NewGuid() });
            await Expect(HttpMethod.Get, "/api/visitor-navigation/location?visitId=2147483647", HttpStatusCode.NotFound, visitor, RoleNames.Visitor);
            await Expect(HttpMethod.Post, "/api/quiz-attempts", HttpStatusCode.NotFound, staff, RoleNames.Visitor, new StartAttemptRequest(entryId));
            using var start = await Send(HttpMethod.Post, "/api/quiz-attempts", visitor, RoleNames.Visitor, new StartAttemptRequest(entryId));
            if (start.StatusCode != HttpStatusCode.OK) throw new Exception("HTTP quiz start failed: " + start.StatusCode);
            using var json = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
            foreach (var question in json.RootElement.GetProperty("data").GetProperty("questions").EnumerateArray()) {
                if (question.GetProperty("isCorrect").ValueKind != JsonValueKind.Null || question.GetProperty("explanation").ValueKind != JsonValueKind.Null)
                    throw new Exception("Unanswered question leaks its result");
                foreach (var option in question.GetProperty("options").EnumerateArray()) if (option.TryGetProperty("isCorrect", out _)) throw new Exception("Option answer key leaked");
            }
            Console.WriteLine("Experience HTTP checks passed: DI, routes, authentication, role policies, validation, response envelope and no quiz answer-key leak.");
        } finally { await app.StopAsync(); }
    }
}

// Test server only. Production continues to use the existing JWT bearer middleware.
sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["X-Test-User"], out var id)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString()) }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
