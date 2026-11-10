using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Account;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

internal static class AssignmentChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        Check(typeof(AccountsController).GetCustomAttribute<AuthorizeAttribute>()?.Roles == RoleNames.Admin, "Only Admin manages assignments");
        var api = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json"))
            .AddJsonFile(Path.Combine(api, "appsettings.Development.json"), true)
            .AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
        var schema = "smgs_assignment_test_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? config.GetConnectionString("DefaultConnection")) { SearchPath = schema, Pooling = false };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connection.ConnectionString));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = new AccountAdminService(users, db);
        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA \"{schema}\"");
        try
        {
            await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            var museum = new Museum { Name = "Assignment fixture" };
            db.Museums.Add(museum); await db.SaveChangesAsync();
            foreach (var role in new[] { RoleNames.MuseumStaff, RoleNames.Curator, RoleNames.Visitor })
            {
                var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = role.Replace(" ", ""), FullName = role, Email = role.Replace(" ", "") + "@example.invalid" };
                Check((await users.CreateAsync(user)).Succeeded, "Create fixture user");
                Check((await users.AddToRoleAsync(user, role)).Succeeded, "Set fixture role");
                var assigned = await service.AssignScopeAsync(user.Id, new ScopeAssignmentRequestDto { MuseumId = museum.Id });
                if (role == RoleNames.Visitor) { Check(assigned.StatusCode == 400, "Visitor cannot be assigned"); continue; }
                Check(assigned.StatusCode == 201 && assigned.Data!.UserRoles.SequenceEqual(new[] { role }), "Assign either eligible role");
                Check((await users.GetRolesAsync(user)).SequenceEqual(new[] { role }), "Assignment preserves role");
                Check((await service.AssignScopeAsync(user.Id, new ScopeAssignmentRequestDto { MuseumId = museum.Id })).StatusCode == 409, "Duplicate rejected");
                var rows = (await service.GetAssignmentsAsync(user.Id, museum.Id, null, null)).Data!;
                Check(rows.Count == 1 && rows[0].UserRoles.SequenceEqual(new[] { role }) && rows[0].AssignedAt != default, "List includes role and date");
                var accounts = (await service.GetAccountsAsync(user.Email, role, null, null, 1, 10)).Data!;
                Check(accounts.Items.Single().Assignments.Single().UserRoles.Contains(role), "Search plus role and nested assignment roles");
                Check((await service.RevokeScopeAsync(rows[0].Id)).StatusCode == 200, "Revoke succeeds");
                Check(!(await service.GetAssignmentsAsync(user.Id, museum.Id, null, null)).Data!.Any(), "Revoke removes assignment");
            }
            Console.WriteLine("PASS: assignment roles, keyword + role filtering, duplicate rejection, role preservation, revoke and Admin policy.");
        }
        finally { await db.Database.ExecuteSqlRawAsync($"DROP SCHEMA \"{schema}\" CASCADE"); }
    }
}
