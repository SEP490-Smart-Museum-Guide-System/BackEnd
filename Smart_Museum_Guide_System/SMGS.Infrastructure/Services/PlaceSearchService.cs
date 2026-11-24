using System.Net;
using System.Text.Json;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Navigation;

namespace SMGS.Infrastructure.Services;

public class PlaceSearchService(HttpClient client, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
{
    public async Task<ResponseDto<List<PlaceResponseDto>>> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        query = query?.Trim();
        if (query is null || query.Length < 3 || query.Length > 200)
            return ResponseDto<List<PlaceResponseDto>>.Fail("Nhập từ 3 đến 200 ký tự.");
        try
        {
            var region = System.Text.RegularExpressions.Regex.Match(query,
                @"^(?:bảo tàng|bao tang)\s+(?:ở|tại|o|tai)\s+(.+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!region.Success && configuration?["VietMap:ApiKey"] is string key)
                return await SearchVietMapAsync(query, Uri.EscapeDataString(key), cancellationToken);
            var path = region.Success
                ? $"api/?q={Uri.EscapeDataString(region.Groups[1].Value)}&osm_tag=tourism:museum&limit=50"
                : $"api/?q={Uri.EscapeDataString(query)}&limit=6";
            using var response = await client.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return ResponseDto<List<PlaceResponseDto>>.Fail("Dịch vụ tìm kiếm đang bận. Vui lòng thử lại sau.", 429);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var places = new List<PlaceResponseDto>();
            foreach (var feature in json.RootElement.GetProperty("features").EnumerateArray())
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

    private async Task<ResponseDto<List<PlaceResponseDto>>> SearchVietMapAsync(string query, string key, CancellationToken token)
    {
        const string api = "https://maps.vietmap.vn/api/";
        using var response = await client.GetAsync($"{api}autocomplete/v4?text={Uri.EscapeDataString(query)}&apikey={key}", token);
        response.EnsureSuccessStatusCode();
        using var suggestions = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var places = new List<PlaceResponseDto>();
        foreach (var item in suggestions.RootElement.EnumerateArray().Take(6))
        {
            var reference = item.GetProperty("ref_id").GetString();
            if (string.IsNullOrWhiteSpace(reference)) continue;
            using var detail = await client.GetAsync($"{api}place/v4?refid={Uri.EscapeDataString(reference)}&apikey={key}", token);
            detail.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(token));
            var place = json.RootElement;
            var address = place.GetProperty("display").GetString() ?? "";
            var name = item.TryGetProperty("name", out var value) ? value.GetString() : null;
            var lat = place.GetProperty("lat").GetDouble();
            var lng = place.GetProperty("lng").GetDouble();
            if (Math.Abs(lat) <= 90 && Math.Abs(lng) <= 180)
                places.Add(new(string.IsNullOrWhiteSpace(name) ? address : name, address, lat, lng));
        }
        return ResponseDto<List<PlaceResponseDto>>.Success(places);
    }
}
