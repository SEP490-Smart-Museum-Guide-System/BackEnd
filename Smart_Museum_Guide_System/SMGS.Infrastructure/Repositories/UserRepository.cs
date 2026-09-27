using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Identity;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Data;
using SMGS.Domain.Constants;

namespace SMGS.Infrastructure.Repositories;

public class UserRepository(UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager, ApplicationDbContext db) : IUserRepository
{
    public async Task<UserAccountDto?> FindByEmailAsync(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        return user is null ? null : new(user.Id, user.Email!, user.FullName);
    }

    public async Task<(UserAccountDto? User, IEnumerable<string> Errors)> CreateWithPasswordAsync(
        string email, string fullName, string password)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName };
        try
        {
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                return (null, result.Errors.Select(error => error.Description).ToArray());

            result = await userManager.AddToRoleAsync(user, RoleNames.Visitor);
            if (!result.Succeeded)
                return (null, result.Errors.Select(error => error.Description).ToArray());

            await transaction.CommitAsync();
            return (new UserAccountDto(user.Id, user.Email!, user.FullName), Array.Empty<string>());
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "EmailIndex" or "UserNameIndex" })
        {
            // Concurrent requests can pass Identity's duplicate check before either insert completes.
            return (null, new[] { "Email đã được sử dụng." });
        }
    }

    public async Task<bool> CheckPasswordAsync(Guid userId, string password)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return false;

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        return result.Succeeded;
    }

    public async Task<IList<string>> GetRolesAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? new List<string>() : await userManager.GetRolesAsync(user);
    }
}