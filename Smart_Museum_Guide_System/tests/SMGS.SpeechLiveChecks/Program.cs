// Runs the actual API/JWT/EF implementation in a temporary PostgreSQL schema.
// No mock data responses, no production-schema migrations, no fake speech bytes.
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SMGS.Infrastructure.Data;

var apiPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
var root = Path.GetFullPath(Path.Combine(apiPath, "../../.."));
var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(apiPath, "appsettings.json"))
    .AddJsonFile(Path.Combine(apiPath, "appsettings.Development.json"), true)
    .AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
var schema = "smgs_speech_ui_test_" + Guid.NewGuid().ToString("N");
var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION")
    ?? config.GetConnectionString("DefaultConnection")) { SearchPath = schema, Pooling = false };
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
var createSchema = $"CREATE SCHEMA \"{schema}\"";
var dropSchema = $"DROP SCHEMA \"{schema}\" CASCADE";
await db.Database.ExecuteSqlRawAsync(createSchema);
Process? api = null, browser = null, frontend = null;
try {
    await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
    using var probe = new TcpListener(IPAddress.Loopback, 0);
    probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
    var address = $"http://127.0.0.1:{port}";
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = apiPath, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SMGS.API.dll"));
    start.ArgumentList.Add("--urls"); start.ArgumentList.Add(address);
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ConnectionStrings__DefaultConnection"] = connection.ConnectionString;
    start.Environment["JwtSettings__Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    start.Environment["Logging__LogLevel__Default"] = "Error";
    start.Environment["Cors__AllowedOrigins__0"] = "http://127.0.0.1:4174";
    start.Environment["CloudflareR2__SeedDemoMedia"] = "false";
    start.Environment["ArtifactSpeech__Enabled"] = "true";
    start.Environment["ArtifactSpeech__BaseUrl"] = "http://127.0.0.1:8000/";
    start.Environment["ArtifactSpeech__ServiceKey"] = File.ReadAllText(Path.Combine(root, "AI-System/state/tts-service-key.txt")).Trim();
    api = Process.Start(start) ?? throw new Exception("Cannot start isolated real API.");
    var logs = api.StandardOutput.ReadToEndAsync(); var errors = api.StandardError.ReadToEndAsync();
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    var ready = false;
    for (var i = 0; i < 100; i++) {
        if (api.HasExited) throw new Exception("Isolated API startup failed; no secret logs printed.");
        try { ready = (await http.GetAsync(address + "/api/museums/cities")).IsSuccessStatusCode; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { }
        if (ready) break;
        await Task.Delay(200);
    }
    if (!ready) throw new Exception("Isolated API readiness timeout.");
    var vite = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root, "FrontEnd/smgs_web"), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in new[] { "node_modules/vite/bin/vite.js", "--host", "127.0.0.1", "--port", "4174", "--strictPort" }) vite.ArgumentList.Add(arg);
    vite.Environment["VITE_API_BASE_URL"] = address + "/api";
    frontend = Process.Start(vite) ?? throw new Exception("Cannot start product UI.");
    var viteLogs = frontend.StandardOutput.ReadToEndAsync(); var viteErrors = frontend.StandardError.ReadToEndAsync();
    var uiReady = false;
    for (var i = 0; i < 60; i++) {
        if (frontend.HasExited) throw new Exception("Product UI startup failed.");
        try { uiReady = (await http.GetAsync("http://127.0.0.1:4174")).IsSuccessStatusCode; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { }
        if (uiReady) break;
        await Task.Delay(150);
    }
    if (!uiReady) throw new Exception("Product UI startup timeout.");
    Console.WriteLine("Real API and temporary PostgreSQL schema ready; starting product UI check.");
    var node = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root, "FrontEnd/smgs_web"), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    node.ArgumentList.Add("tools/check-studio-speech-ui.mjs");
    node.Environment["SMGS_SPEECH_TEST_API"] = address;
    browser = Process.Start(node) ?? throw new Exception("Cannot start product UI check.");
    var browserOutput = browser.StandardOutput.ReadToEndAsync(); var browserErrors = browser.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    await browser.WaitForExitAsync(timeout.Token);
    Console.WriteLine(await browserOutput);
    if (browser.ExitCode != 0) Console.WriteLine(await browserErrors);
    if (browser.ExitCode != 0) throw new Exception("Product UI speech check failed.");
} finally {
    foreach (var process in new[] { browser, frontend, api }) {
        if (process is null) continue;
        if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        process.Dispose();
    }
    // The schema identifier is generated here, never user input or public/application schema.
    await db.Database.ExecuteSqlRawAsync(dropSchema);
    Console.WriteLine("Temporary speech test schema removed; existing application schema untouched.");
}
