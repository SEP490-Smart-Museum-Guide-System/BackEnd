using Microsoft.AspNetCore.Identity;
using SMGS.Domain.Constants;
using SMGS.Infrastructure.Identity;

namespace SMGS.Infrastructure.Data;

// Development demo accounts, invoked by the existing Development startup seeder.
public static class AccountSeeder
{
    public const string Password = "Abc123@";

    public static async Task<(ApplicationUser Admin, ApplicationUser MuseumStaff, ApplicationUser Curator)> SeedAsync(UserManager<ApplicationUser> userManager)
    {
        var admin = await EnsureUserAsync(userManager, "admin@museum.vn", "Quản trị viên Hệ thống", Password, RoleNames.Admin);
        var museumStaff = await EnsureUserAsync(userManager, "staff@museum.vn", "Nhân viên Bảo tàng", Password, RoleNames.MuseumStaff);
        var curator = await EnsureUserAsync(userManager, "curator@museum.vn", "Chuyên gia Duyệt nội dung", Password, RoleNames.Curator);
        _ = await EnsureUserAsync(userManager, "support@museum.vn", "Nhân viên Hỗ trợ", Password, RoleNames.Staff);
        return (admin, museumStaff, curator);
    }

    public static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, string password, string role)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null) return existing;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createRes = await userManager.CreateAsync(user, password);
            if (!createRes.Succeeded)
                throw new InvalidOperationException($"Could not seed user {email}: {string.Join("; ", createRes.Errors.Select(e => e.Description))}");

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded) throw new InvalidOperationException("Could not assign seeded account role.");
            return user;
        }


}
