using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

/// <summary>Server-to-server adapter for the local ONNX language model. No cloud provider is called.</summary>
public class LocalArtifactGuideModel(HttpClient http, IOptions<GuideModelOptions> options) : IArtifactGuideModel
{
    public async Task<GuideModelAnswer> AnswerAsync(GuideContext context, string question, IReadOnlyList<GuideTurn> history,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.ServiceKey)
            || !Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https") || baseUri.UserInfo.Length > 0
            || baseUri.Query.Length > 0 || baseUri.Fragment.Length > 0 || baseUri.AbsolutePath != "/")
            throw new GuideException("Trợ lý chưa được kết nối model local. Bạn vẫn có thể xem và nghe nội dung hiện vật.");
        // URL/key are server configuration, never client input. Redirects are disabled in DI.
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "api/v1/guide/answers"));
        request.Headers.Add("X-Service-Key", config.ServiceKey);
        request.Content = JsonContent.Create(new { context, question, history });
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new GuideException("Trợ lý local đang bận. Vui lòng thử lại sau.", 429);
            if ((int)response.StatusCode == 422)
                throw new GuideException("Nội dung hoặc hội thoại quá dài cho model local. Hãy bắt đầu cuộc trò chuyện mới hoặc xem bản thuyết minh.", 422);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
                throw new GuideException("Trợ lý local phản hồi chậm. Vui lòng thử lại.", 504);
            if (!response.IsSuccessStatusCode)
                throw new GuideException("Chưa thể kết nối model local. Vui lòng thử lại sau.", response.StatusCode == HttpStatusCode.BadGateway ? 502 : 503);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (root.EnumerateObject().Count() != 2) throw new JsonException();
            var supported = root.GetProperty("supported").GetBoolean();
            var answer = root.GetProperty("answer").GetString();
            if (answer is null || answer.Length > 4000 || (supported && string.IsNullOrWhiteSpace(answer))) throw new JsonException();
            return new(answer, supported);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new GuideException("Trợ lý local phản hồi chậm. Vui lòng thử lại.", 504); }
        catch (HttpRequestException) { throw new GuideException("Chưa thể kết nối model local. Vui lòng thử lại sau."); }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new GuideException("Trợ lý trả về nội dung không hợp lệ. Vui lòng thử lại.", 502); }
    }
}
