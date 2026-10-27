using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMGS.Application.Interfaces;
using SMGS.Application.Services;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Repositories;
using SMGS.Infrastructure.Services;

namespace SMGS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection for Museum.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(settings => Encoding.UTF8.GetByteCount(settings.Key) >= 32,
                "JwtSettings:Key must contain at least 32 UTF-8 bytes.")
            .ValidateOnStart();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMuseumRepository, MuseumRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMuseumService, MuseumService>();
        services.AddScoped<OutdoorNavigationService>();
        services.AddHttpClient<PlaceSearchService>(client =>
        {
            client.BaseAddress = new Uri((configuration["Photon:BaseUrl"] ?? "https://photon.komoot.io/").TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.MaxResponseContentBufferSize = 1024 * 1024;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SMGS/1.0");
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<IOutdoorRoutingService, OsrmRoutingService>(client =>
        {
            var baseUrl = configuration["Osrm:BaseUrl"] ?? throw new InvalidOperationException("Configure Osrm:BaseUrl.");
            if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
                || (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new InvalidOperationException("Osrm:BaseUrl must be an HTTP(S) URL.");
            client.BaseAddress = uri;
            client.Timeout = TimeSpan.FromSeconds(10);
            client.MaxResponseContentBufferSize = 8 * 1024 * 1024;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SMGS/1.0");
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IArtifactService, ArtifactService>();
        services.AddScoped<IIndoorMapService, IndoorMapService>();
        services.AddSingleton<IndoorImageStore>();
        services.AddScoped<IArtifactContentRepository, ArtifactContentRepository>();
        services.AddScoped<IArtifactContentService, ArtifactContentService>();
        services.AddSingleton<IQrCodeService, QrCodeService>();
        return services;
    }
}
