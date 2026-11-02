using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMGS.Application.DTOs.Auth.Request;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Infrastructure;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

internal static class RoleChecks
{
    public static async Task RunAsync()
    {
        var apiPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(apiPath, "appsettings.json"))
            .AddJsonFile(Path.Combine(apiPath, "appsettings.Development.json"))
            .AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
        ServiceProvider CreateProvider(bool rejectRole = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthentication();
            services.AddInfrastructure(config);
            if (rejectRole) services.AddScoped<UserManager<ApplicationUser>, RejectRoleUserManager>();
            return services.BuildServiceProvider();
        }
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var requiredRoles = new[] { RoleNames.Visitor, RoleNames.Admin, RoleNames.Staff, RoleNames.MuseumStaff, RoleNames.Curator };
        foreach (var role in requiredRoles)
            if (!await roles.RoleExistsAsync(role)) throw new Exception($"Missing role: {role}");

        var request = new RegisterRequestDto
        {
            Email = $"role-check-{Guid.NewGuid():N}@example.invalid",
            FullName = "Temporary Role Check",
            Password = $"Test1-{Guid.NewGuid():N}"
        };
        try
        {
            // Simulate an Identity role assignment failure after the user insert.
            using (var failingProvider = CreateProvider(rejectRole: true))
            using (var failingScope = failingProvider.CreateScope())
            {
                var failed = await failingScope.ServiceProvider.GetRequiredService<IAuthService>().RegisterAsync(request);
                if (failed.StatusCode != 400 || failed.Data is not null)
                    throw new Exception("Role assignment failure must reject registration without a JWT.");
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync(request.Email) is not null)
                throw new Exception("Role assignment failure left a user without a role.");

            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var registered = await auth.RegisterAsync(request);
            if (registered.StatusCode != 201) throw new Exception("Registration failed after rollback.");
            var user = await users.FindByEmailAsync(request.Email) ?? throw new Exception("User missing.");
            var assignedRoles = await users.GetRolesAsync(user);
            if (assignedRoles.Count != 1 || assignedRoles[0] != RoleNames.Visitor)
                throw new Exception("New account must have exactly the Visitor role.");
            var login = await auth.LoginAsync(new LoginRequestDto { Email = request.Email, Password = request.Password });
            if (login.StatusCode != 200) throw new Exception("Visitor login failed.");
            var jwt = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
            foreach (var token in new[] { registered.Data!.AccessToken, login.Data!.AccessToken })
            {
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, jwt.CreateValidationParameters(), out _);
                if (!principal.IsInRole(RoleNames.Visitor) || requiredRoles.Skip(1).Any(principal.IsInRole))
                    throw new Exception("JWT must contain Visitor without elevated roles.");
            }
            Console.WriteLine("PASS: all five database roles, default Visitor membership/JWT and registration rollback on role failure.");
        }
        finally
        {
            using var cleanupScope = provider.CreateScope();
            var users = cleanupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(request.Email);
            if (user is not null && !(await users.DeleteAsync(user)).Succeeded)
                throw new Exception("Could not remove temporary role test user.");
        }
    }
}

internal sealed class RejectRoleUserManager(IServiceProvider services) : UserManager<ApplicationUser>(
    services.GetRequiredService<IUserStore<ApplicationUser>>(),
    services.GetRequiredService<IOptions<IdentityOptions>>(),
    services.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
    services.GetServices<IUserValidator<ApplicationUser>>(),
    services.GetServices<IPasswordValidator<ApplicationUser>>(),
    services.GetRequiredService<ILookupNormalizer>(),
    services.GetRequiredService<IdentityErrorDescriber>(),
    services,
    services.GetRequiredService<ILogger<UserManager<ApplicationUser>>>())
{
    public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
        => Task.FromResult(IdentityResult.Failed(new IdentityError { Description = "Simulated role assignment failure." }));
}