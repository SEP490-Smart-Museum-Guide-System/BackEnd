using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

public class ArtifactSpeechOptions
{
    public const string SectionName = "ArtifactSpeech";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:8000/";
    public string ServiceKey { get; set; } = "";
}

public class LocalArtifactSpeech(HttpClient http, IOptions<ArtifactSpeechOptions> options) : IArtifactSpeech
{
    public async Task<SpeechAudio> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.ServiceKey)
            || !Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/")
            throw new GuideException("Bộ đọc tiếng Việt chưa được kết nối. Vui lòng thử lại sau.");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(uri, "api/v1/speech"));
        request.Headers.Add("X-Service-Key", config.ServiceKey);
        request.Content = JsonContent.Create(new { text, language = "vi-VN", speed = 1.0 });
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new GuideException("Bộ đọc tiếng Việt đang bận hoặc chưa sẵn sàng. Vui lòng thử lại.", (int)response.StatusCode == 429 ? 429 : 503);
            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (response.Content.Headers.ContentType?.MediaType != "audio/wav" || data.Length < 46
                || !data.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !data.AsSpan(8, 4).SequenceEqual("WAVE"u8))
                throw new GuideException("Bộ đọc chưa trả về âm thanh WAV hợp lệ.", 502);
            return new(data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new GuideException("Tạo giọng đọc quá lâu. Vui lòng thử lại.", 504); }
        catch (HttpRequestException) { throw new GuideException("Chưa kết nối được bộ đọc tiếng Việt."); }
    }
}
