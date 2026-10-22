using SMGS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using SMGS.Domain.Constants;

namespace SMGS.Infrastructure.Data;

public static class DataSeeder
{
    public static IdentityRole<Guid>[] CreateRoleSeeds() =>
    [
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3005"), Name = RoleNames.Curator, NormalizedName = "CURATOR", ConcurrencyStamp = "seed-curator-v1" },
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3001"), Name = RoleNames.Visitor, NormalizedName = "VISITOR", ConcurrencyStamp = "seed-visitor-v1" },
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3002"), Name = RoleNames.Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "seed-admin-v1" },
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3003"), Name = RoleNames.Staff, NormalizedName = "STAFF", ConcurrencyStamp = "seed-staff-v1" },
        new() { Id = new Guid("c1e5f237-c782-4910-9235-5d73ecdd3004"), Name = RoleNames.MuseumStaff, NormalizedName = "MUSEUM STAFF", ConcurrencyStamp = "seed-museum-staff-v1" }
    ];

    public static async Task EnsureRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in CreateRoleSeeds())
        {
            if (await roles.RoleExistsAsync(role.Name!)) continue;
            var result = await roles.CreateAsync(role);
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not initialize required account roles.");
        }
    }

    // Explicit development command only; passwords are hashed by Identity.
    public static async Task CreateUserAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var email = configuration["BootstrapUser:Email"]?.Trim();
        var password = configuration["BootstrapUser:Password"];
        var fullName = configuration["BootstrapUser:FullName"]?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fullName) || fullName.Length > 200)
            throw new InvalidOperationException("Set BootstrapUser:Email, Password and FullName (1-200 characters) in user secrets.");

        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("This account already exists; no changes were made.");

        var (user, errors) = await users.CreateWithPasswordAsync(email, fullName, password);
        if (user is null)
            throw new InvalidOperationException(string.Join("; ", errors));
    }
}
