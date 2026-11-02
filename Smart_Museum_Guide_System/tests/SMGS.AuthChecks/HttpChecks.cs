using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SMGS.Application.DTOs.Auth.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Infrastructure;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;

internal static class HttpChecks
{
    public static async Task RunAsync(bool hold)
    {
        var apiPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(apiPath, "appsettings.json"))
            .AddJsonFile(Path.Combine(apiPath, "appsettings.Development.json"), true)
            .AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
        var schema = "smgs_auth_http_test_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION")
            ?? config.GetConnectionString("DefaultConnection")) { SearchPath = schema, Pooling = false };
        config["ConnectionStrings:DefaultConnection"] = connection.ConnectionString;
        config["JwtSettings:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var services = new ServiceCollection();
        services.AddLogging(); services.AddAuthentication(); services.AddInfrastructure(config);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createSql = $"CREATE SCHEMA \"{schema}\""; // Fixed prefix + Guid N, not input.
        var dropSql = $"DROP SCHEMA \"{schema}\" CASCADE";
        await db.Database.ExecuteSqlRawAsync(createSql);
        Process? api = null;
        try
        {
            await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            await db.Roles.Where(role => role.Name == "Curator").ExecuteDeleteAsync();
            await DataSeeder.EnsureRolesAsync(provider);
            await DataSeeder.EnsureRolesAsync(provider);
            Check(await db.Roles.CountAsync() == 5, "Role initialization must repair missing roles without duplication.");
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var staff = new ApplicationUser { UserName = "admin@auth-test.invalid", Email = "admin@auth-test.invalid", FullName = "Temporary Admin" };
            Check((await users.CreateAsync(staff, " TestPass1 ")).Succeeded, "Test staff creation failed.");
            Check((await users.AddToRoleAsync(staff, "Admin")).Succeeded, "Test admin role failed.");
            using var portProbe = new TcpListener(IPAddress.Loopback, 0);
            portProbe.Start(); var port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
            var baseUrl = $"http://127.0.0.1:{port}";
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = apiPath,
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SMGS.API.dll"));
            start.ArgumentList.Add("--urls"); start.ArgumentList.Add(baseUrl);
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["ConnectionStrings__DefaultConnection"] = connection.ConnectionString;
            start.Environment["JwtSettings__Key"] = config["JwtSettings:Key"];
            start.Environment["Logging__LogLevel__Default"] = "Error";
            api = Process.Start(start) ?? throw new Exception("Cannot start isolated API.");
            // Drain redirected pipes; auth data and test tokens are never printed.
            var output = api.StandardOutput.ReadToEndAsync(); var errors = api.StandardError.ReadToEndAsync();
            using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(3) };
            var ready = false;
            for (var attempt = 0; attempt < 80; attempt++)
            {
                if (api.HasExited) throw new Exception("Isolated API exited before startup.");
                try { ready = (await http.GetAsync("/api/museums/cities")).IsSuccessStatusCode; }
                catch (HttpRequestException) { }
                if (ready) break;
                await Task.Delay(150);
            }
            Check(ready, "Isolated API startup timed out.");
            using (var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login"))
            {
                preflight.Headers.Add("Origin", "http://localhost:5173");
                preflight.Headers.Add("Access-Control-Request-Method", "POST");
                preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
                var response = await http.SendAsync(preflight);
                Check(response.StatusCode == HttpStatusCode.NoContent &&
                    response.Headers.GetValues("Access-Control-Allow-Origin").Single() == "http://localhost:5173", "CORS preflight failed.");
            }
            using (var denied = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login"))
            {
                denied.Headers.Add("Origin", "https://untrusted.invalid");
                denied.Headers.Add("Access-Control-Request-Method", "POST");
                Check(!(await http.SendAsync(denied)).Headers.Contains("Access-Control-Allow-Origin"), "Untrusted CORS origin allowed.");
            }
            foreach (var endpoint in new[] { "login", "register" })
                Check((await http.PostAsJsonAsync($"/api/auth/{endpoint}", new { })).StatusCode == HttpStatusCode.BadRequest, "Invalid auth input accepted.");
            var request = new { email = "visitor@auth-test.invalid", fullName = "Ngọc An", password = " TestPass1 ", roles = new[] { "Admin" } };
            var registration = await http.PostAsJsonAsync("/api/auth/register", request);
            Check(registration.StatusCode == HttpStatusCode.Created, "HTTP registration failed.");
            var registered = (await registration.Content.ReadFromJsonAsync<ResponseDto<TokenResponseDto>>())!.Data!;
            Check(registered.Roles.SequenceEqual(new[] { "Visitor" }) && registered.FullName == request.fullName
                && !string.IsNullOrEmpty(registered.AccessToken) && registered.AccessTokenExpiration > DateTime.UtcNow, "Incomplete/privileged registration response.");
            Check((await http.PostAsJsonAsync("/api/auth/register", request)).StatusCode == HttpStatusCode.BadRequest, "Duplicate registration accepted.");
            var login = await http.PostAsJsonAsync("/api/auth/login", new { request.email, request.password });
            Check(login.StatusCode == HttpStatusCode.OK, "HTTP login failed.");
            Check((await login.Content.ReadFromJsonAsync<ResponseDto<TokenResponseDto>>())!.Data!.Roles.Single() == "Visitor", "HTTP login role mismatch.");
            Check((await http.PostAsJsonAsync("/api/auth/login", new { request.email, password = request.password.Trim() })).StatusCode == HttpStatusCode.Unauthorized,
                "Password whitespace must be preserved.");
            var adminLogin = await http.PostAsJsonAsync("/api/auth/login", new { email = staff.Email, password = " TestPass1 " });
            Check((await adminLogin.Content.ReadFromJsonAsync<ResponseDto<TokenResponseDto>>())!.Data!.Roles.Single() == "Admin", "Staff role response mismatch.");
            Console.WriteLine("PASS: real HTTP register/login, PostgreSQL Identity, duplicate/invalid credentials, password whitespace, Visitor-only registration and CORS allow/deny.");
            if (hold)
            {
                Console.WriteLine($"AUTH_TEST_BASE_URL={baseUrl}");
                Console.WriteLine("Press Enter to stop the fixture and remove its isolated schema.");
                await Console.In.ReadLineAsync();
            }
        }
        finally
        {
            if (api is not null)
            {
                if (!api.HasExited) { api.Kill(entireProcessTree: true); await api.WaitForExitAsync(); }
                api.Dispose();
            }
            await db.Database.ExecuteSqlRawAsync(dropSql);
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
