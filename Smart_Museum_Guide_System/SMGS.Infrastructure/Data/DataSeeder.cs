using SMGS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SMGS.Infrastructure.Data;

public static class DataSeeder
{
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