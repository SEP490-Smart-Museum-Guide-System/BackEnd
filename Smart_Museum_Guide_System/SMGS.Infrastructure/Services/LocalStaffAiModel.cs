using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SMGS.Application.DTOs.Guide;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

public class LocalStaffAiModel(HttpClient http, IOptionsMonitor<GuideModelOptions> options) : IStaffAiModel
{
    public async Task<JsonElement> GenerateAsync(StaffAiModelRequest data, CancellationToken cancellationToken = default)
    {
        var config = options.Get("StaffAi");
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.ServiceKey) || !Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/")
            throw new GuideException("Model AI local chưa được kết nối. Chưa tạo bản nháp nào.");
        var path = data.Mode == "quiz" && data.Count > 1 ? "api/v1/studio/quiz-sets" : "api/v1/studio/drafts";
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(uri, path));
        request.Headers.Add("X-Service-Key", config.ServiceKey);
        request.Content = JsonContent.Create(data);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new GuideException("AI đang bận. Hãy thử lại sau.", 429);
            if ((int)response.StatusCode == 422)
            {
                var detail = await ReadSafeDetail(response, cancellationToken);
                throw new GuideException(detail ?? "Dịch vụ AI không chấp nhận dữ liệu yêu cầu. Nếu lỗi lặp lại, hãy báo quản trị viên kiểm tra kết nối AI.", 422);
            }
            if (response.StatusCode == HttpStatusCode.BadGateway) throw new GuideException("AI chưa tạo được bản nháp đủ căn cứ. Hãy bổ sung tư liệu và tạo lại.", 502);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout) throw new GuideException("Model phản hồi chậm. Vui lòng thử lại.", 504);
            if (!response.IsSuccessStatusCode) throw new GuideException("Model local chưa sẵn sàng. Vui lòng thử lại.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.Clone();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new GuideException("Model phản hồi chậm. Vui lòng thử lại.", 504); }
        catch (HttpRequestException) { throw new GuideException("Chưa thể kết nối model AI local."); }
        catch (JsonException) { throw new GuideException("Phản hồi model không hợp lệ.", 502); }
    }

    private static async Task<string?> ReadSafeDetail(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("detail", out var value) && value.ValueKind == JsonValueKind.String)
            {
                var detail = value.GetString()?.Trim();
                if (!string.IsNullOrEmpty(detail) && detail.Length <= 500) return detail;
            }
        }
        catch (JsonException) { }
        return null;
    }
}
