using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using SMGS.Application.DTOs.Guide;
using SMGS.Infrastructure.Services;

internal static class SpeechChecks {
    public static async Task RunAsync() {
        var handler = new SpeechHandler(); using var http = new HttpClient(handler);
        var options = new ArtifactSpeechOptions(); var provider = new LocalArtifactSpeech(http, Options.Create(options));
        async Task Expect(int status) {
            try { await provider.GenerateAsync("Bản thuyết minh tiếng Việt."); throw new Exception("Expected speech failure."); }
            catch (GuideException e) { if (e.StatusCode != status || e.Message.Contains("PRIVATE_PROVIDER")) throw new Exception("Unsafe speech error."); }
        }
        await Expect(503); if (handler.Calls != 0) throw new Exception("Disabled TTS contacted service.");
        options.Enabled = true; options.ServiceKey = "test-not-real"; options.BaseUrl = "file:///models/";
        await Expect(503); if (handler.Calls != 0) throw new Exception("Invalid TTS URL contacted service.");
        options.BaseUrl = "http://127.0.0.1:8000/";
        var result = await provider.GenerateAsync("Bản đọc đã sửa.");
        if (result.Bytes.Length != 46 || handler.Url != "http://127.0.0.1:8000/api/v1/speech" || handler.Key != options.ServiceKey || !handler.Body!.Contains("vi-VN")) throw new Exception("TTS provider contract failed.");
        handler.Status = HttpStatusCode.ServiceUnavailable; await Expect(503);
        handler.Status = HttpStatusCode.TooManyRequests; await Expect(429);
        handler.Status = HttpStatusCode.OK; handler.Type = "text/html"; await Expect(502);
        handler.Type = "audio/wav"; handler.Broken = true; await Expect(502);
        Console.WriteLine("PASS: speech provider and HTTP role/scope/audio contract checks (fixtures, no real DB).");
    }
    private class SpeechHandler : HttpMessageHandler {
        public int Calls; public string? Key, Url, Body; public string Type = "audio/wav";
        public HttpStatusCode Status = HttpStatusCode.OK; public bool Broken;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Calls++; Key = request.Headers.GetValues("X-Service-Key").Single(); Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var bytes = new byte[46]; if (!Broken) { "RIFF"u8.CopyTo(bytes); "WAVE"u8.CopyTo(bytes.AsSpan(8)); }
            var content = new ByteArrayContent(bytes); content.Headers.ContentType = new MediaTypeHeaderValue(Type);
            return new(Status) { Content = content };
        }
    }
}
