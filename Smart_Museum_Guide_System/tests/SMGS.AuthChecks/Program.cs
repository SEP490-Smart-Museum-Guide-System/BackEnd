using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SMGS.Application.DTOs.Auth.Request;
using SMGS.Application.Interfaces;
using SMGS.Application.Services;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var settings = new JwtSettings
{
    Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    Issuer = "museum-tests",
    Audience = "museum-client-tests",
    ExpireMinutes = 60
};
var tokens = new TokenService(Options.Create(settings));
var repo = new TestUserRepository();
var service = new AuthService(repo, tokens);
var request = new LoginRequestDto { Email = "visitor@example.com", Password = "correct-password1" };
Check((await service.LoginAsync(new LoginRequestDto())).StatusCode == 400, "Missing input must fail.");
Check((await service.LoginAsync(new LoginRequestDto { Email = "invalid", Password = "x" })).StatusCode == 400,
    "Invalid email must fail.");
Check(repo.Lookups == 0, "Invalid input must not query the database.");
repo.Exists = false;
var unknown = await service.LoginAsync(request);
Check(unknown.StatusCode == 401 && unknown.Data is null, "Unknown user must not get a token.");
repo.Exists = true;
repo.PasswordValid = false;
var wrong = await service.LoginAsync(request);
Check(wrong.StatusCode == 401 && wrong.Data is null && wrong.Message == unknown.Message,
    "Wrong password must return the same generic error as an unknown account.");
Check(repo.RoleReads == 0, "Failed login must stop before token generation.");
repo.PasswordValid = true;
var success = await service.LoginAsync(request);
Check(success.StatusCode == 200 && success.Data is not null, "Correct credentials must log in.");
var data = success.Data!;
var handler = new JwtSecurityTokenHandler();
var principal = handler.ValidateToken(data.AccessToken, settings.CreateValidationParameters(), out _);
Check(principal.FindFirstValue(ClaimTypes.NameIdentifier) == repo.User.Id.ToString(), "User ID claim missing.");
Check(principal.FindFirstValue(ClaimTypes.Email) == repo.User.Email, "Email claim missing.");
Check(principal.IsInRole("Visitor"), "Role claim missing.");
Check(data.Email == repo.User.Email && data.FullName == repo.User.FullName, "Login profile mismatch.");
Check(data.Roles.SequenceEqual(new[] { "Visitor" }), "Response must carry actual roles.");
Check(data.AccessTokenExpiration > DateTime.UtcNow, "Token must have a future expiry.");
var second = await service.LoginAsync(request);
Check(second.Data!.AccessToken != data.AccessToken, "Each login needs a unique JWT ID.");

var registrationRepo = new TestUserRepository { Exists = false };
var registrationService = new AuthService(registrationRepo, tokens);
Check((await registrationService.RegisterAsync(new RegisterRequestDto())).StatusCode == 400,
    "Registration requires email, full name and password.");
Check((await registrationService.RegisterAsync(new RegisterRequestDto
{
    Email = "new@example.com", FullName = "   ", Password = "Valid123"
})).StatusCode == 400, "Whitespace full name must fail.");
Check(registrationRepo.Lookups == 0, "Invalid registration must not query the database.");
var registration = new RegisterRequestDto
{
    Email = "new@example.com", FullName = "  New Visitor  ", Password = "Valid123"
};
registrationRepo.CreationSucceeds = false;
var rejectedRegistration = await registrationService.RegisterAsync(registration);
Check(rejectedRegistration.StatusCode == 400 && rejectedRegistration.Data is null,
    "Identity creation failure must not issue a token.");
Check(registrationRepo.RoleReads == 0, "Failed registration must not generate tokens.");
registrationRepo.CreationSucceeds = true;
var registered = await registrationService.RegisterAsync(registration);
Check(registered.StatusCode == 201 && registered.Data?.FullName == "New Visitor",
    "Registration must return 201 with normalized profile.");
var registeredPrincipal = handler.ValidateToken(registered.Data!.AccessToken, settings.CreateValidationParameters(), out _);
Check(registeredPrincipal.FindFirstValue(ClaimTypes.Email) == registration.Email, "Registration JWT email mismatch.");
var createCalls = registrationRepo.CreateCalls;
var duplicate = await registrationService.RegisterAsync(registration);
Check(duplicate.StatusCode == 400 && duplicate.Data is null && registrationRepo.CreateCalls == createCalls,
    "Duplicate email must fail before creating another user.");

void Reject(string token, TokenValidationParameters validation)
{
    try { handler.ValidateToken(token, validation, out _); }
    catch (SecurityTokenException) { return; }
    throw new Exception("Invalid JWT was accepted.");
}
var wrongAudience = settings.CreateValidationParameters();
wrongAudience.ValidAudience = "another-client";
Reject(data.AccessToken, wrongAudience);
var wrongIssuer = settings.CreateValidationParameters();
wrongIssuer.ValidIssuer = "another-issuer";
Reject(data.AccessToken, wrongIssuer);
var wrongKey = settings.CreateValidationParameters();
wrongKey.IssuerSigningKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
Reject(data.AccessToken, wrongKey);
var expired = new JwtSecurityToken(settings.Issuer, settings.Audience, expires: DateTime.UtcNow.AddMinutes(-1),
    signingCredentials: new SigningCredentials(settings.CreateValidationParameters().IssuerSigningKey,
        SecurityAlgorithms.HmacSha256));
Reject(handler.WriteToken(expired), settings.CreateValidationParameters());
Reject(handler.WriteToken(new JwtSecurityToken(settings.Issuer, settings.Audience,
    expires: DateTime.UtcNow.AddMinutes(1))), settings.CreateValidationParameters());

var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
    ["JwtSettings:Key"] = settings.Key,
    ["JwtSettings:Issuer"] = settings.Issuer,
    ["JwtSettings:Audience"] = settings.Audience
}).Build();
var identityUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = request.Email, Email = request.Email, LockoutEnabled = true };
identityUser.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(identityUser, request.Password);
var services = new ServiceCollection();
services.AddLogging();
services.AddAuthentication();
services.AddInfrastructure(configuration);
services.AddSingleton<IUserStore<ApplicationUser>>(new MemoryUserStore(identityUser));
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
Check(await userRepository.CheckPasswordAsync(identityUser.Id, request.Password), "Identity must verify a hashed password.");
for (var attempt = 0; attempt < 5; attempt++)
    Check(!await userRepository.CheckPasswordAsync(identityUser.Id, "wrong"), "Wrong password accepted.");
Check(identityUser.LockoutEnd > DateTimeOffset.UtcNow, "Five failures must lock the account.");
Check(!await userRepository.CheckPasswordAsync(identityUser.Id, request.Password), "Locked user must not log in.");
identityUser.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1);
Check(await userRepository.CheckPasswordAsync(identityUser.Id, request.Password), "Login must resume after lockout expires.");
Check(identityUser.AccessFailedCount == 0, "Successful login must reset failed attempts.");
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
Check(db.Database.GetMigrations().Any(), "Identity migration missing.");
var identityTables = db.Model.GetEntityTypes().Select(entity => entity.GetTableName()).ToHashSet();
Check(new[] { "Users", "Roles", "UserRoles", "UserClaims", "UserLogins", "UserTokens", "RoleClaims" }.All(identityTables.Contains)
    && !identityTables.Any(name => name?.StartsWith("AspNet", StringComparison.Ordinal) == true),
    "Identity tables must use names without the AspNet prefix.");
_ = scope.ServiceProvider.GetRequiredService<IAuthService>();
Check(provider.GetRequiredService<IOptions<JwtSettings>>().Value.Key == settings.Key, "JWT options did not bind.");
configuration["JwtSettings:Key"] = "short";
var invalidServices = new ServiceCollection();
invalidServices.AddLogging();
invalidServices.AddInfrastructure(configuration);
using var invalidProvider = invalidServices.BuildServiceProvider();
try
{
    _ = invalidProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
    throw new Exception("Short signing key accepted.");
}
catch (OptionsValidationException) { }
Console.WriteLine("PASS: registration validation/creation/duplicate handling, login validation, failures, Identity password hashing/lockout, JWT claims/signature/issuer/audience/expiry, DI and migration metadata.");

if (args.Contains("--database")) await RoleChecks.RunAsync();
if (args.Contains("--http")) await HttpChecks.RunAsync(args.Contains("--hold"));

sealed class TestUserRepository : IUserRepository
{
    public UserAccountDto User { get; private set; } = new(Guid.NewGuid(), "visitor@example.com", "Museum Visitor");
    public bool Exists { get; set; } = true;
    public bool PasswordValid { get; set; } = true;
    public bool CreationSucceeds { get; set; } = true;
    public int CreateCalls { get; private set; }
    public Task<(UserAccountDto? User, IEnumerable<string> Errors)> CreateWithPasswordAsync(string email, string fullName, string password)
    {
        CreateCalls++;
        if (!CreationSucceeds)
            return Task.FromResult<(UserAccountDto?, IEnumerable<string>)>((null, new[] { "Identity validation failed." }));
        User = new(Guid.NewGuid(), email, fullName);
        Exists = true;
        return Task.FromResult<(UserAccountDto?, IEnumerable<string>)>((User, Array.Empty<string>()));
    }
    public int Lookups { get; private set; }
    public int RoleReads { get; private set; }
    public Task<UserAccountDto?> FindByEmailAsync(string email)
    {
        Lookups++;
        return Task.FromResult<UserAccountDto?>(Exists && email == User.Email ? User : null);
    }
    public Task<bool> CheckPasswordAsync(Guid id, string password) => Task.FromResult(PasswordValid);
    public Task<IList<string>> GetRolesAsync(Guid id)
    {
        RoleReads++;
        return Task.FromResult<IList<string>>(new[] { "Visitor" });
    }
}

// In-memory Identity store: exercises the real UserManager and SignInManager without a database server.
sealed class MemoryUserStore(ApplicationUser user) : IUserPasswordStore<ApplicationUser>, IUserLockoutStore<ApplicationUser>, IUserEmailStore<ApplicationUser>
{
    public Task<string?> GetEmailAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.Email);
    public Task SetEmailAsync(ApplicationUser u, string? email, CancellationToken ct) { u.Email = email; return Task.CompletedTask; }
    public Task<bool> GetEmailConfirmedAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.EmailConfirmed);
    public Task SetEmailConfirmedAsync(ApplicationUser u, bool confirmed, CancellationToken ct) { u.EmailConfirmed = confirmed; return Task.CompletedTask; }
    public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct) => Task.FromResult<ApplicationUser?>(email == user.NormalizedEmail ? user : null);
    public Task<string?> GetNormalizedEmailAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.NormalizedEmail);
    public Task SetNormalizedEmailAsync(ApplicationUser u, string? email, CancellationToken ct) { u.NormalizedEmail = email; return Task.CompletedTask; }
    public void Dispose() { }
    public Task<string> GetUserIdAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.Id.ToString());
    public Task<string?> GetUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.UserName);
    public Task SetUserNameAsync(ApplicationUser u, string? name, CancellationToken ct) { u.UserName = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(ApplicationUser u, string? name, CancellationToken ct) { u.NormalizedUserName = name; return Task.CompletedTask; }
    public Task<IdentityResult> CreateAsync(ApplicationUser u, CancellationToken ct) => throw new NotSupportedException();
    public Task<IdentityResult> UpdateAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityResult> DeleteAsync(ApplicationUser u, CancellationToken ct) => throw new NotSupportedException();
    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult<ApplicationUser?>(id == user.Id.ToString() ? user : null);
    public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult<ApplicationUser?>(name == user.NormalizedUserName ? user : null);
    public Task SetPasswordHashAsync(ApplicationUser u, string? hash, CancellationToken ct) { u.PasswordHash = hash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash);
    public Task<bool> HasPasswordAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash is not null);
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnd);
    public Task SetLockoutEndDateAsync(ApplicationUser u, DateTimeOffset? end, CancellationToken ct) { u.LockoutEnd = end; return Task.CompletedTask; }
    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(++u.AccessFailedCount);
    public Task ResetAccessFailedCountAsync(ApplicationUser u, CancellationToken ct) { u.AccessFailedCount = 0; return Task.CompletedTask; }
    public Task<int> GetAccessFailedCountAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnabled);
    public Task SetLockoutEnabledAsync(ApplicationUser u, bool enabled, CancellationToken ct) { u.LockoutEnabled = enabled; return Task.CompletedTask; }
}
