using System.Net;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using SMGS.Application.DTOs.Museum.Request;
using SMGS.Application.DTOs.Navigation;
using SMGS.Application.Services;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Services;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
await PlaceChecks.RunAsync();
var origin = new CoordinateDto { Latitude = 10.7626, Longitude = 106.6824 };
var destination = new CoordinateDto { Latitude = 10.7769, Longitude = 106.7009 };
const string valid = """{"code":"Ok","routes":[{"distance":3500.5,"duration":720.25,"geometry":{"type":"LineString","coordinates":[[106.6824,10.7626],[106.7009,10.7769]]}}]}""";
var handler = new RoutingHandler(valid);
using var client = new HttpClient(handler) { BaseAddress = new Uri("https://routing.invalid/") };
var provider = new OsrmRoutingService(client, NullLogger<OsrmRoutingService>.Instance);
var previousCulture = CultureInfo.CurrentCulture;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
var result = await provider.GetRouteAsync(origin, destination);
CultureInfo.CurrentCulture = previousCulture;
Check(result.StatusCode == 200 && result.Data?.DistanceMeters == 3500.5, "Route units or precision lost.");
Check(result.Data!.RoutePoints[0].Latitude == origin.Latitude && result.Data.RoutePoints[0].Longitude == origin.Longitude, "GeoJSON lon/lat reversed.");
Check(handler.Path!.Contains("106.6824,10.7626;106.7009,10.7769") && handler.Path.Contains("geometries=geojson"), "OSRM coordinates must be invariant lon/lat.");
foreach (var (body, status, expected) in new[]
{
    ("{\"code\":\"NoRoute\"}", HttpStatusCode.OK, 422),
    ("{\"code\":\"NoSegment\"}", HttpStatusCode.BadRequest, 422),
    ("<html>offline</html>", HttpStatusCode.OK, 502),
    ("{}", HttpStatusCode.OK, 502),
    (valid.Replace("106.6824", "900"), HttpStatusCode.OK, 502),
    (valid.Replace("3500.5", "-1"), HttpStatusCode.OK, 502),
    (valid.Replace("[[106.6824,10.7626],[106.7009,10.7769]]", "[]"), HttpStatusCode.OK, 502),
    ("", HttpStatusCode.TooManyRequests, 429),
    ("", HttpStatusCode.GatewayTimeout, 504),
    ("", HttpStatusCode.ServiceUnavailable, 502)
})
{
    handler.Body = body; handler.Status = status;
    Check((await provider.GetRouteAsync(origin, destination)).StatusCode == expected, "Provider failure not mapped.");
}
handler.Failure = new HttpRequestException("Contains private coordinates");
Check((await provider.GetRouteAsync(origin, destination)).StatusCode == 502, "Network failure not mapped.");
handler.Failure = new TaskCanceledException();
Check((await provider.GetRouteAsync(origin, destination)).StatusCode == 504, "Timeout not mapped.");
using (var cancel = new CancellationTokenSource())
{
    cancel.Cancel();
    try { await provider.GetRouteAsync(origin, destination, cancel.Token); throw new Exception("Cancellation swallowed."); }
    catch (OperationCanceledException) { }
}
handler.Failure = null; handler.Body = valid; handler.Status = HttpStatusCode.OK;
var museums = new Museums();
var navigation = new OutdoorNavigationService(museums, provider);
var request = new OutdoorRouteRequestDto { MuseumId = 7, Origin = origin };
Check((await navigation.GetRouteAsync(request)).Data?.MuseumId == 7, "Museum ID missing.");
request.TravelMode = "WALK";
Check((await navigation.GetRouteAsync(request)).StatusCode == 400, "Walking must not use driving.");
request.TravelMode = "DRIVE";
museums.Item!.IsActive = false;
Check((await navigation.GetRouteAsync(request)).StatusCode == 404, "Inactive destination exposed.");
museums.Item.IsActive = true; museums.Item.Latitude = null;
Check((await navigation.GetRouteAsync(request)).StatusCode == 400, "Missing destination accepted.");
museums.Item = null;
Check((await navigation.GetRouteAsync(request)).StatusCode == 404, "Missing museum accepted.");
foreach (var invalid in new[] { new CoordinateDto(), new CoordinateDto { Latitude = double.NaN, Longitude = 0 },
    new CoordinateDto { Latitude = 91, Longitude = 0 }, new CoordinateDto { Latitude = 0, Longitude = -181 } })
{
    request.Origin = invalid;
    Check((await navigation.GetRouteAsync(request)).StatusCode == 400, "Invalid origin accepted.");
}
var discovery = new MuseumService(museums);
Check(CityCatalog.Items.Count == 34 && CityCatalog.Items.Select(c => c.Code).Distinct().Count() == 34, "City codes inconsistent.");
foreach (var query in new[]
{
    new DiscoverMuseumsRequestDto { CityCode = "HCM" },
    new DiscoverMuseumsRequestDto { Latitude = 10 },
    new DiscoverMuseumsRequestDto { Latitude = double.PositiveInfinity, Longitude = 0 },
    new DiscoverMuseumsRequestDto { SortBy = "distance" },
    new DiscoverMuseumsRequestDto { SortBy = "invalid" },
    new DiscoverMuseumsRequestDto { Page = int.MaxValue },
    new DiscoverMuseumsRequestDto { PageSize = 51 }
}) Check((await discovery.DiscoverAsync(query, false)).StatusCode == 400, "Invalid discovery accepted.");
await discovery.DiscoverAsync(new() { IsActive = false, Search = "Museum", CityCode = "79" }, false);
Check(museums.Query!.IsActive == true && museums.Query.Keyword == "Museum", "Visitor scope or search alias lost.");
await discovery.DiscoverAsync(new() { IsActive = false }, true);
Check(museums.Query!.IsActive == false, "Admin inactive filter lost.");
museums.Item = new() { Id = 7, Name = "Museum", CityCode = "79" };
Check((await discovery.UpdateAsync(7, new() { Name = "Museum" })).StatusCode == 200
    && museums.Item.CityCode == "79", "Legacy update must preserve existing city code.");
Console.WriteLine("PASS: discovery validation, visitor visibility, OSRM geometry, errors, cancellation and travel modes.");
if (args.Contains("--database")) await DatabaseChecks.RunAsync();
if (args.Contains("--live-osrm"))
{
    // Fixed public demo coordinates from the task, never a visitor's GPS.
    using var liveClient = new HttpClient { BaseAddress = new Uri("https://router.project-osrm.org/"), Timeout = TimeSpan.FromSeconds(15) };
    liveClient.DefaultRequestHeaders.UserAgent.ParseAdd("SMGS/1.0");
    var live = await new OsrmRoutingService(liveClient, NullLogger<OsrmRoutingService>.Instance).GetRouteAsync(origin, destination);
    Check(live.StatusCode == 200 && live.Data?.RoutePoints.Count > 1, $"Live OSRM demo smoke check failed (HTTP {live.StatusCode}).");
    Console.WriteLine($"PASS: live OSRM demo returned {live.Data!.DistanceMeters} meters, {live.Data.DurationSeconds} seconds, {live.Data.RoutePoints.Count} points.");
}

internal sealed class RoutingHandler(string body) : HttpMessageHandler
{
    public string Body = body;
    public HttpStatusCode Status = HttpStatusCode.OK;
    public Exception? Failure;
    public string? Path;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Path = request.RequestUri!.PathAndQuery;
        if (Failure is not null) throw Failure;
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
    }
}

internal sealed class Museums : IMuseumRepository
{
    public Museum? Item = new() { Id = 7, Name = "Museum", Latitude = 10.7769m, Longitude = 106.7009m };
    public MuseumDiscoveryQuery? Query;
    public Task<Museum?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Item);
    public Task<(IList<MuseumDiscoveryItem> Items, int TotalCount)> DiscoverAsync(MuseumDiscoveryQuery query, CancellationToken cancellationToken = default)
    { Query = query; return Task.FromResult(((IList<MuseumDiscoveryItem>)new List<MuseumDiscoveryItem>(), 0)); }
    public Task<(IList<Museum> Items, int TotalCount)> GetPagedAsync(string? k, bool? a, int p, int s) => throw new NotSupportedException();
    public Task<bool> ExistsByNameAsync(string n, int? excludeId = null) => Task.FromResult(false);
    public Task<Museum> CreateAsync(Museum museum) => throw new NotSupportedException();
    public Task UpdateAsync(Museum museum) => Task.CompletedTask;
}
