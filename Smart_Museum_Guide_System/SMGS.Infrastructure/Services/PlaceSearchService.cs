using System.Net;
using System.Text.Json;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Navigation;

namespace SMGS.Infrastructure.Services;

public class PlaceSearchService(HttpClient client)
{
    public async Task<ResponseDto<List<PlaceResponseDto>>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        query = query?.Trim();
        if (query is null || query.Length < 3 || query.Length > 200)
            return ResponseDto<List<PlaceResponseDto>>.Fail("Nhập từ 3 đến 200 ký tự.");
        try
        {
            using var response = await client.GetAsync($"api/?q={Uri.EscapeDataString(query)}&limit=6", cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return ResponseDto<List<PlaceResponseDto>>.Fail("Dịch vụ tìm kiếm đang bận. Vui lòng thử lại sau.", 429);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var places = new List<PlaceResponseDto>();
            foreach (var feature in json.RootElement.GetProperty("features").EnumerateArray().Take(6))
            {
                var geometry = feature.GetProperty("geometry");
                if (geometry.GetProperty("type").GetString() != "Point") continue;
                var coordinates = geometry.GetProperty("coordinates");
                var lon = coordinates[0].GetDouble();
                var lat = coordinates[1].GetDouble();
                if (!double.IsFinite(lat) || !double.IsFinite(lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180) continue;
                var properties = feature.GetProperty("properties");
                string Text(string key) => properties.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
                var address = string.Join(", ", new[] { Text("housenumber"), Text("street"), Text("district"), Text("city"), Text("state"), Text("country") }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
                var name = Text("name");
                if (string.IsNullOrWhiteSpace(name)) name = address;
                if (!string.IsNullOrWhiteSpace(name)) places.Add(new(name, address, lat, lon));
            }
            return ResponseDto<List<PlaceResponseDto>>.Success(places);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResponseDto<List<PlaceResponseDto>>.Fail("Tìm kiếm quá thời gian. Vui lòng thử lại.", 504);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or FormatException)
        {
            return ResponseDto<List<PlaceResponseDto>>.Fail("Không kết nối được dịch vụ tìm địa điểm. Vui lòng thử lại.", 502);
        }
    }
}
