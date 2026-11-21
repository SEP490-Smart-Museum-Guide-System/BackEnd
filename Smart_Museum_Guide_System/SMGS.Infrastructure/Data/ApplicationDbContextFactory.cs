using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SMGS.Infrastructure.Data;

/// <summary>Allows schema tooling without starting the API or running development seeders.</summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var apiPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../SMGS.API"));
        if (!Directory.Exists(apiPath))
        {
            apiPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Smart_Museum_Guide_System/SMGS.API"));
        }
        if (!Directory.Exists(apiPath))
        {
            apiPath = Directory.GetCurrentDirectory();
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=smgs_db;Username=postgres;Password=123456";
        return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }
}
