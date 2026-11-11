using Microsoft.EntityFrameworkCore;
using SMGS.Infrastructure.Data;

if (args.Length == 2 && args[0] == "--export-schema") {
    await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=localhost;Database=schema_export").Options);
    await File.WriteAllTextAsync(args[1], db.Database.GenerateCreateScript());
    Console.WriteLine("Exported current model schema; no database connection opened.");
    return;
}
await TourChecks.Run();
