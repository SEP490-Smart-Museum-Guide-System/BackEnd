using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Navigation;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

public class OsrmRoutingService(HttpClient client, ILogger<OsrmRoutingService> logger) : IOutdoorRoutingService
{
    public async Task<ResponseDto<OutdoorRouteResponseDto>> GetRouteAsync(CoordinateDto origin, CoordinateDto destination,
        CancellationToken cancellationToken = default)
    {
        if (!origin.IsValid() || !destination.IsValid())
            return ResponseDto<OutdoorRouteResponseDto>.Fail("Tọa độ không hợp lệ.");
        var coordinates = string.Create(CultureInfo.InvariantCulture,
            $"{origin.Longitude},{origin.Latitude};{destination.Longitude},{destination.Latitude}");
        try
        {
            using var response = await client.GetAsync(
                $"route/v1/driving/{coordinates}?overview=full&geometries=geojson&steps=false", cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return ResponseDto<OutdoorRouteResponseDto>.Fail("Dịch vụ định tuyến đang bận. Vui lòng thử lại sau.", 429);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
                return ResponseDto<OutdoorRouteResponseDto>.Fail("Dịch vụ định tuyến phản hồi quá chậm. Vui lòng thử lại.", 504);
            // OSRM can return NoRoute/NoSegment with a non-2xx status.
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
            {
                logger.LogWarning("Routing provider returned HTTP {StatusCode}.", (int)response.StatusCode);
                return InvalidProvider();
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            var code = root.GetProperty("code").GetString();
            if (code is "NoRoute" or "NoSegment")
                return ResponseDto<OutdoorRouteResponseDto>.Fail("Không tìm thấy đường ô tô tới bảo tàng.", 422);
            if (!response.IsSuccessStatusCode || code != "Ok") return InvalidProvider();
            var routes = root.GetProperty("routes");
            if (routes.GetArrayLength() == 0) return InvalidProvider();
            var route = routes[0];
            var distance = route.GetProperty("distance").GetDouble();
            var duration = route.GetProperty("duration").GetDouble();
            if (!double.IsFinite(distance) || !double.IsFinite(duration) || distance < 0 || duration < 0)
                return InvalidProvider();
            var geometry = route.GetProperty("geometry");
            if (geometry.GetProperty("type").GetString() != "LineString") return InvalidProvider();
            var points = new List<CoordinateDto>();
            foreach (var point in geometry.GetProperty("coordinates").EnumerateArray())
            {
                if (point.GetArrayLength() < 2) return InvalidProvider();
                var coordinate = new CoordinateDto { Latitude = point[1].GetDouble(), Longitude = point[0].GetDouble() };
                if (!coordinate.IsValid()) return InvalidProvider();
                points.Add(coordinate);
            }
            if (points.Count < 2) return InvalidProvider();
            return ResponseDto<OutdoorRouteResponseDto>.Success(new()
            {
                Destination = destination, DistanceMeters = distance, DurationSeconds = duration, RoutePoints = points
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
        ResponseDto<OutdoorRouteResponseDto>.Fail("Không thể tải tuyến đường. Bạn vẫn có thể mở Google Maps.", 502);
}
