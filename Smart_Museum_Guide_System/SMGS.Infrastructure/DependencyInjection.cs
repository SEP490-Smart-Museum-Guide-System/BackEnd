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
        services.AddOptions<SMGS.Application.DTOs.Commerce.SePayOptions>().Bind(configuration.GetSection("SePay"));
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
        services.AddScoped<IAccountAdminService, AccountAdminService>();
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
        services.AddScoped<ITopicService, TopicService>();
        services.AddSingleton<Amazon.S3.IAmazonS3>(_ => {
            var account = configuration["CloudflareR2:AccountId"];
            var key = configuration["CloudflareR2:AccessKeyId"];
            var secret = configuration["CloudflareR2:SecretAccessKey"];
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
                throw new MediaStorageException();
            if (!System.Text.RegularExpressions.Regex.IsMatch(account, "^[a-fA-F0-9]{32}$")) throw new MediaStorageException();
            var endpoint = configuration["CloudflareR2:Endpoint"] ?? $"https://{account}.r2.cloudflarestorage.com";
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.Host.EndsWith(".r2.cloudflarestorage.com") || uri.AbsolutePath != "/") throw new MediaStorageException();
            return new Amazon.S3.AmazonS3Client(key, secret, new Amazon.S3.AmazonS3Config {
                ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "auto", MaxErrorRetry = 2, Timeout = TimeSpan.FromSeconds(60)
            });
        });
        services.AddSingleton<IFileStorageService, R2FileStorageService>();
        services.AddSingleton<ArtifactAssetStore>();
        services.AddSingleton<MuseumImageStore>();
        services.AddScoped<IArtifactAssetService, ArtifactAssetService>();
        services.AddScoped<ICommerceService, CommerceService>();
        services.AddScoped<IIndoorMapService, IndoorMapService>();
        services.AddSingleton<IndoorImageStore>();
        services.AddScoped<IArtifactContentRepository, ArtifactContentRepository>();
        services.AddScoped<IArtifactContentService, ArtifactContentService>();
        services.AddOptions<GuideModelOptions>().Bind(configuration.GetSection(GuideModelOptions.SectionName));
        services.AddHttpClient<IArtifactGuideModel, LocalArtifactGuideModel>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(55);
            client.MaxResponseContentBufferSize = 256 * 1024;
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IArtifactGuideService, ArtifactGuideService>();
        services.AddScoped<IStaffAiSource, StaffAiSourceReader>();
        services.AddScoped<IStaffAiService, StaffAiService>();
        services.AddOptions<ArtifactSpeechOptions>().Bind(configuration.GetSection(ArtifactSpeechOptions.SectionName));
        services.AddHttpClient<IArtifactSpeech, LocalArtifactSpeech>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(65);
            client.MaxResponseContentBufferSize = 32 * 1024 * 1024;
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<IStaffAiModel, LocalStaffAiModel>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(105);
            client.MaxResponseContentBufferSize = 256 * 1024;
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddScoped<IVisitRepository, VisitRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<IComplaintRepository, ComplaintRepository>();
        services.AddScoped<IVisitService, VisitService>();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<SMGS.Application.DTOs.Experience.ExperienceOptions>().Bind(configuration.GetSection("VisitorExperience"))
            .ValidateDataAnnotations().Validate(o => {
                try { TimeZoneInfo.FindSystemTimeZoneById(o.TimeZoneId); return true; } catch { return false; }
            }, "VisitorExperience:TimeZoneId is invalid.").ValidateOnStart();
        services.AddScoped<ITourService, TourService>();
        services.AddScoped<IVisitProgressService, VisitProgressService>();
        services.AddScoped<IRecognitionService, RecognitionService>();
        services.AddScoped<IVisitorNavigationService, VisitorNavigationService>();
        services.AddScoped<IQuestionBankService, QuestionBankService>();
        services.AddScoped<IDailyQuizService, DailyQuizService>();
        services.AddScoped<IQuizAttemptService, QuizAttemptService>();
        services.AddScoped<IGamificationService, GamificationService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IComplaintService, ComplaintService>();
        return services;
    }
}
