using System.Net;
using SMGS.Infrastructure.Services;

internal static class PlaceChecks
{
    public static async Task RunAsync()
    {
        using var handler = new PlaceHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://places.invalid/") };
        var service = new PlaceSearchService(client);
        if ((await service.SearchAsync("ab")).StatusCode != 400 || handler.Calls != 0) throw new Exception("Short input reached provider.");
        if ((await service.SearchAsync(new string('x', 201))).StatusCode != 400) throw new Exception("Long input accepted.");
        var response = await service.SearchAsync("café & park");
        if (response.Data?.Single().Latitude != 10.7 || response.Data.Single().Longitude != 106.7 || !handler.Path!.Contains("%26")) throw new Exception("Place coordinates or query encoding incorrect.");
        handler.Body = "{\"features\":[]}";
        if ((await service.SearchAsync("missing")).Data?.Count != 0) throw new Exception("Empty results failed.");
        handler.Body = "{}";
        if ((await service.SearchAsync("invalid")).StatusCode != 502) throw new Exception("Invalid provider data accepted.");
        handler.Status = HttpStatusCode.TooManyRequests;
        if ((await service.SearchAsync("limited")).StatusCode != 429) throw new Exception("Quota failure lost.");
        Console.WriteLine("PASS: place search validation, encoding, coordinates, empty results and provider errors.");
    }
    private sealed class PlaceHandler : HttpMessageHandler
    {
        public int Calls;
        public string? Path;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Body = """{"features":[{"geometry":{"type":"Point","coordinates":[106.7,10.7]},"properties":{"name":"Park","city":"HCM"}}]}""";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Path = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
        }
    }
}
