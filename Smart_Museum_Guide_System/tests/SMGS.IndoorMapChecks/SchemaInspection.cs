using Microsoft.Extensions.Configuration;
using Npgsql;

internal static class SchemaInspection
{
    public static async Task Run()
    {
        var api = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SMGS.API"));
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json"))
            .AddJsonFile(Path.Combine(api, "appsettings.Development.json"), true)
            .AddUserSecrets("smgs-api-local-development").AddEnvironmentVariables().Build();
        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION") ?? config.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, tx)) await readOnly.ExecuteNonQueryAsync();
        foreach (var query in new[]
        {
            "SELECT table_name FROM information_schema.tables WHERE table_schema = current_schema() ORDER BY table_name",
            "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'museums' ORDER BY column_name",
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            "SELECT 'floors=' || count(*) FROM floors",
            "SELECT 'museums=' || count(*) FROM museums"
        })
        {
            await using var command = new NpgsqlCommand(query, connection, tx);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) Console.WriteLine(reader.GetString(0));
        }
        await tx.RollbackAsync();
    }
}
