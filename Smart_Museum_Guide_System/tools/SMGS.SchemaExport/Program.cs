using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SMGS.Infrastructure.Data;

var output = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? "../project-docs");
Directory.CreateDirectory(output);
await using var db = new ApplicationDbContextFactory().CreateDbContext([]);
var tables = db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables.OrderBy(t => t.Name).ToList();
string Q(string name) => "\"" + name.Replace("\"", "\\\"") + "\"";
string Cols(IEnumerable<IColumn> columns) => "(" + string.Join(", ", columns.Select(c => Q(c.Name))) + ")";
var text = new StringBuilder("// Generated from ApplicationDbContext by tools/SMGS.SchemaExport.\n// Backend only; AI Vision / Scan manage separate schemas. SQL is authoritative for filtered indexes/defaults/checks.\n\n");
foreach (var table in tables)
{
    text.AppendLine($"Table {Q(table.Name)} {{");
    foreach (var column in table.Columns)
        text.AppendLine($"  {Q(column.Name)} {Q(column.StoreType)}{(column.IsNullable ? "" : " [not null]")}");
    text.AppendLine("  indexes {");
    if (table.PrimaryKey != null) text.AppendLine($"    {Cols(table.PrimaryKey.Columns)} [pk]");
    foreach (var index in table.Indexes.OrderBy(i => i.Name))
        text.AppendLine($"    {Cols(index.Columns)} [name: {Q(index.Name)}{(index.IsUnique ? ", unique" : "")}]");
    text.AppendLine("  }\n}\n");
}
foreach (var table in tables)
foreach (var fk in table.ForeignKeyConstraints.OrderBy(f => f.Name))
    text.AppendLine($"Ref: {Q(table.Name)}.{Cols(fk.Columns)} > {Q(fk.PrincipalTable.Name)}.{Cols(fk.PrincipalColumns)}");
await File.WriteAllTextAsync(Path.Combine(output, "smgs_backend.dbml"), text.ToString(), new UTF8Encoding(false));
await File.WriteAllTextAsync(Path.Combine(output, "smgs_current_schema.sql"),
    "-- Fresh/empty databases only. Read DATABASE_SETUP.md for existing databases.\n" +
    db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent), new UTF8Encoding(false));
await File.WriteAllTextAsync(Path.Combine(output, "smgs_schema_inventory.json"), JsonSerializer.Serialize(tables.Select(t => new
{
    table = t.Name,
    columns = t.Columns.Select(c => new { name = c.Name, type = c.StoreType, nullable = c.IsNullable }),
    primaryKey = t.PrimaryKey?.Columns.Select(c => c.Name),
    foreignKeys = t.ForeignKeyConstraints.Select(f => new { columns = f.Columns.Select(c => c.Name), parent = f.PrincipalTable.Name, parentColumns = f.PrincipalColumns.Select(c => c.Name) })
}), new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Exported {tables.Count} tables and columns/keys to {output}.");

if (args.Contains("--verify"))
{
    var connection = Environment.GetEnvironmentVariable("SMGS_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Set SMGS_TEST_CONNECTION for isolated PostgreSQL verification.");
    var schema = "smgs_migration_test_" + Guid.NewGuid().ToString("N");
    var builder = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
    await using var test = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(builder.ConnectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options);
    var createSchema = $"CREATE SCHEMA \"{schema}\""; // schema is entirely generated above, never input
    var dropSchema = $"DROP SCHEMA \"{schema}\" CASCADE";
    await test.Database.ExecuteSqlRawAsync(createSchema);
    try
    {
        await test.Database.MigrateAsync();
        await test.Database.MigrateAsync();
        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = @schema", conn);
        query.Parameters.AddWithValue("schema", schema);
        var actual = new HashSet<(string, string)>();
        await using (var reader = await query.ExecuteReaderAsync())
            while (await reader.ReadAsync()) actual.Add((reader.GetString(0), reader.GetString(1)));
        foreach (var table in tables)
        foreach (var column in table.Columns)
            if (!actual.Contains((table.Name, column.Name))) throw new Exception($"Missing migrated column {table.Name}.{column.Name}");
        if (actual.Select(c => c.Item1).Distinct().Count(t => t != "__EFMigrationsHistory") != tables.Count)
            throw new Exception("Migrated table count differs from EF model.");
        if ((await test.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Pending migrations after baseline.");
        Console.WriteLine($"PASS fresh migration + repeat migration: {tables.Count} tables, all model columns present.");
    }
    finally { await test.Database.ExecuteSqlRawAsync(dropSchema); }
}
