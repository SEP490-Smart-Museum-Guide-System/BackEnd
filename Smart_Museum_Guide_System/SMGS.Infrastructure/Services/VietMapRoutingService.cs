using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Navigation;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

public class VietMapRoutingService(HttpClient client, Microsoft.Extensions.Configuration.IConfiguration configuration, ILogger<VietMapRoutingService> logger) : IOutdoorRoutingService
{
    public async Task<ResponseDto<OutdoorRouteResponseDto>> GetRouteAsync(CoordinateDto origin, CoordinateDto destination,
        CancellationToken cancellationToken = default)
    {
        if (!origin.IsValid() || !destination.IsValid())
            return ResponseDto<OutdoorRouteResponseDto>.Fail("Tọa độ không hợp lệ.");
        var key = configuration["VietMap:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return InvalidProvider();
        var coordinates = string.Create(CultureInfo.InvariantCulture,
            $"point={origin.Latitude},{origin.Longitude}&point={destination.Latitude},{destination.Longitude}");
        try
        {
            using var response = await client.GetAsync(
                $"api/route/v4?{coordinates}&vehicle=car&points_encoded=false&apikey={Uri.EscapeDataString(key)}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return ResponseDto<OutdoorRouteResponseDto>.Fail("Dịch vụ định tuyến đang bận. Vui lòng thử lại sau.", 429);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
                return ResponseDto<OutdoorRouteResponseDto>.Fail("Dịch vụ định tuyến phản hồi quá chậm. Vui lòng thử lại.", 504);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
            {
                logger.LogWarning("Routing provider returned HTTP {StatusCode}.", (int)response.StatusCode);
                return InvalidProvider();
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            var code = root.GetProperty("code").GetString();
            if (code == "ERROR_UNKNOWN" && root.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.String && messages.GetString() is string message
                && message.Contains("out of bounds", StringComparison.OrdinalIgnoreCase))
            {
                var point = message.StartsWith("Point 0 ", StringComparison.OrdinalIgnoreCase) ? "Điểm xuất phát" : "Điểm đến";
                return ResponseDto<OutdoorRouteResponseDto>.Fail($"{point} nằm ngoài vùng chỉ đường của VietMap.", 422);
            }
            if (code == "ZERO_RESULTS")
                return ResponseDto<OutdoorRouteResponseDto>.Fail("Không tìm thấy đường ô tô tới bảo tàng.", 422);
            if (!response.IsSuccessStatusCode || code != "OK") return InvalidProvider();
            var routes = root.GetProperty("paths");
            if (routes.GetArrayLength() == 0) return InvalidProvider();
            var route = routes[0];
            var distance = route.GetProperty("distance").GetDouble();
            var duration = route.GetProperty("time").GetDouble() / 1000;
            if (!double.IsFinite(distance) || !double.IsFinite(duration) || distance < 0 || duration < 0)
                return InvalidProvider();
            var points = new List<CoordinateDto>();
            foreach (var point in route.GetProperty("points").GetProperty("coordinates").EnumerateArray())
            {
                if (point.GetArrayLength() < 2) return InvalidProvider();
                var coordinate = new CoordinateDto { Latitude = point[1].GetDouble(), Longitude = point[0].GetDouble() };
                if (!coordinate.IsValid()) return InvalidProvider();
                points.Add(coordinate);
            }
            if (points.Count < 2) return InvalidProvider();
            var steps = new List<RouteStepDto>();
            foreach (var instruction in route.GetProperty("instructions").EnumerateArray())
            {
                var interval = instruction.GetProperty("interval");
                if (interval.GetArrayLength() != 2) return InvalidProvider();
                var index = interval[0].GetInt32();
                if (index < 0 || index >= points.Count) return InvalidProvider();
                steps.Add(new(index, instruction.GetProperty("sign").GetInt32(),
                    instruction.GetProperty("text").GetString() ?? ""));
            }
            return ResponseDto<OutdoorRouteResponseDto>.Success(new()
            {
                Destination = destination, DistanceMeters = distance, DurationSeconds = duration, RoutePoints = points, Steps = steps
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Routing provider timed out.");
            return ResponseDto<OutdoorRouteResponseDto>.Fail("Dịch vụ định tuyến phản hồi quá chậm.", 504);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException
            or InvalidOperationException or FormatException or OverflowException)
        {
            // Exception messages and URLs may include visitor GPS coordinates.
            logger.LogWarning("Routing provider failed ({ErrorType}).", exception.GetType().Name);
            return InvalidProvider();
        }
    }

    private static ResponseDto<OutdoorRouteResponseDto> InvalidProvider() =>
        ResponseDto<OutdoorRouteResponseDto>.Fail("Không thể tải tuyến đường. Vui lòng thử lại.", 502);
}
