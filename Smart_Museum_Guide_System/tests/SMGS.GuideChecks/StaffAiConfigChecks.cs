using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;
using SMGS.Infrastructure;
using SMGS.Infrastructure.Services;

internal static class StaffAiConfigChecks
{
    public static async Task RunAsync()
    {
        foreach (var separate in new[] { true, false })
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
                ["ArtifactGuide:Enabled"] = "true",
                ["ArtifactGuide:BaseUrl"] = "https://guide.example/",
                ["ArtifactGuide:ServiceKey"] = "guide-test-key"
            };
            if (separate)
            {
                values["StaffAi:Enabled"] = "true";
                values["StaffAi:BaseUrl"] = "https://quiz.example/";
                values["StaffAi:ServiceKey"] = "staff-test-key";
            }
            var handler = new Provider();
            var services = new ServiceCollection();
            services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
            services.AddHttpClient<IStaffAiModel, LocalStaffAiModel>()
                .ConfigurePrimaryHttpMessageHandler(() => handler);
            using var provider = services.BuildServiceProvider();
            await provider.GetRequiredService<IStaffAiModel>().GenerateAsync(
                new StaffAiModelRequest(1, "test", "source", "quiz", "general", "vivid", "medium", []));
            var expectedHost = separate ? "quiz.example" : "guide.example";
            var expectedKey = separate ? "staff-test-key" : "guide-test-key";
            if (handler.Url != $"https://{expectedHost}/api/v1/studio/drafts" || handler.Key != expectedKey)
                throw new Exception("Staff AI must use its own configuration, with legacy guide fallback.");
        }
        Console.WriteLine("Staff AI separate configuration and legacy fallback: PASS");
    }

    private sealed class Provider : HttpMessageHandler
    {
        public string? Url;
        public string? Key;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Url = request.RequestUri?.ToString();
            Key = request.Headers.GetValues("X-Service-Key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }
}
