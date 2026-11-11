using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

public class PersonalizedTourOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8004/";
    public string ServiceKey { get; set; } = "";
}

public class PersonalizedTourModel(HttpClient http, IOptions<PersonalizedTourOptions> options) : IPersonalizedTourModel
{
    public async Task<TourModelResult> Recommend(TourModelRequest request, CancellationToken ct)
    {
        var config = options.Value;
        if (config.ServiceKey.Length < 32 || !Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            throw new ExperienceException("Dịch vụ cá nhân hóa chưa được cấu hình. Bạn vẫn có thể chọn tour có sẵn hoặc tham quan tự do.", 503);
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(uri, "api/v1/tours/recommend"));
        message.Headers.Add("X-Service-Key", config.ServiceKey);
        message.Content = JsonContent.Create(request);
        try {
            using var response = await http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
                throw new ExperienceException("Dịch vụ cá nhân hóa đang bận. Vui lòng thử lại hoặc chọn tour có sẵn.", 503);
            return await response.Content.ReadFromJsonAsync<TourModelResult>(cancellationToken: ct)
                ?? throw new ExperienceException("Kết quả cá nhân hóa không hợp lệ.", 502);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new ExperienceException("Cá nhân hóa mất quá nhiều thời gian. Vui lòng thử lại.", 503);
        }
        catch (HttpRequestException) { throw new ExperienceException("Chưa kết nối được dịch vụ cá nhân hóa. Bạn vẫn có thể dùng tour có sẵn.", 503); }
        catch (System.Text.Json.JsonException) { throw new ExperienceException("Kết quả cá nhân hóa không hợp lệ.", 502); }
    }
}
