using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SkiaSharp;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Domain.Constants;
using SMGS.Infrastructure.Services;

internal static class HttpChecks
{
    public static async Task Run(string connection, int mapId, int museumId, Guid staff)
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        var settings = new JwtSettings { Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), Issuer = "indoor-test", Audience = "indoor-test" };
        var tokens = new TokenService(Options.Create(settings));
        var storage = Path.Combine(Path.GetTempPath(), "smgs-indoor-http-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API")),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SMGS.API.dll")); start.ArgumentList.Add("--urls"); start.ArgumentList.Add($"http://127.0.0.1:{port}");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ConnectionStrings__DefaultConnection"] = connection;
        start.Environment["JwtSettings__Key"] = settings.Key;
        start.Environment["JwtSettings__Issuer"] = settings.Issuer;
        start.Environment["JwtSettings__Audience"] = settings.Audience;
        start.Environment["IndoorMaps__StoragePath"] = storage;
        start.Environment["Logging__LogLevel__Default"] = "Error";
        using var process = Process.Start(start) ?? throw new Exception("Fixture API failed to start");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(5) };
            for (var attempt = 0; ; attempt++)
            {
                if (process.HasExited || attempt == 60) throw new Exception("API startup failed");
                try { if ((await http.GetAsync("api/museums/cities")).IsSuccessStatusCode) break; } catch (HttpRequestException) { }
                await Task.Delay(150);
            }
            var path = $"api/indoor-maps/{mapId}";
            Check((await http.GetAsync(path + "/draft")).StatusCode == HttpStatusCode.Unauthorized, "anonymous draft blocked");
            Check((await http.GetAsync(path + "/published")).StatusCode == HttpStatusCode.Unauthorized, "anonymous published blocked");
            void Login(Guid id, string role) => http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(id, "fixture@example.invalid", "Fixture", [role]).Token);
            Login(staff, RoleNames.Visitor);
            Check((await http.GetAsync(path + "/draft")).StatusCode == HttpStatusCode.Forbidden, "visitor draft blocked");
            Login(Guid.NewGuid(), RoleNames.MuseumStaff);
            Check((await http.GetAsync(path + "/draft")).StatusCode == HttpStatusCode.Forbidden, "foreign staff blocked");
            Login(staff, RoleNames.MuseumStaff);
            Check((await http.PostAsJsonAsync($"api/museums/{museumId}/indoor-staff", new { userId = staff })).StatusCode == HttpStatusCode.Forbidden, "staff cannot assign itself");
            var map = (await http.GetFromJsonAsync<ResponseDto<MapDocument>>(path + "/draft"))!.Data!;
            using var bitmap = new SKBitmap(100, 50); using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            async Task<HttpResponseMessage> Upload(byte[] bytes)
            {
                using var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(bytes), "file", "plan.png");
                content.Add(new StringContent(map.Revision.ToString()), "revision"); content.Add(new StringContent("true"), "confirm");
                return await http.PostAsync(path + "/image", content);
            }
            Check((await Upload("fake"u8.ToArray())).StatusCode == HttpStatusCode.BadRequest, "fake PNG blocked");
            var upload = await Upload(png.ToArray());
            Check(upload.IsSuccessStatusCode, "multipart upload"); map = (await upload.Content.ReadFromJsonAsync<ResponseDto<MapDocument>>())!.Data!;
            var fetched = await http.GetAsync(path + "/image?key=" + map.ImageKey);
            Check(fetched.IsSuccessStatusCode && fetched.Content.Headers.ContentType!.MediaType == "image/png", "private image response");
            Check((await http.GetAsync(path + "/image?key=old.png")).StatusCode == HttpStatusCode.Conflict, "stale image blocked");
            var mapQr = await http.GetAsync(path + "/qr/" + map.Markers[0].Id);
            Check(mapQr.IsSuccessStatusCode, "artifact QR download from map");
            var artifactQr = await http.GetByteArrayAsync($"api/artifacts/{map.Markers[0].ArtifactId}/qr-image");
            Check((await mapQr.Content.ReadAsByteArrayAsync()).SequenceEqual(artifactQr), "map and artifact download must encode the same existing QR");
            var publish = await http.PostAsJsonAsync(path + "/publish", new { map.Revision });
            Check(publish.IsSuccessStatusCode, "HTTP publish");
            Login(staff, RoleNames.Visitor);
            Check((await http.GetAsync(path + "/published")).IsSuccessStatusCode, "visitor published read");
            Check((await http.GetAsync(path + "/published/image")).IsSuccessStatusCode, "visitor published image");
            Check((await http.GetAsync(path + "/image")).StatusCode == HttpStatusCode.Forbidden, "visitor draft image blocked");
            Console.WriteLine("PASS: real HTTP role/museum authorization, multipart image validation, private image, stale image, QR download and published reads.");
        }
        finally
        {
            if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); }
            await Task.WhenAll(output, errors);
            // Only the generated fixture directory under the OS temp directory is removed.
            if (Path.GetDirectoryName(storage) == Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) && Directory.Exists(storage)) Directory.Delete(storage, true);
        }
    }
    private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
}
