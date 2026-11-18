using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

internal static class CollectionChecks
{
    public static async Task Run(ApplicationDbContext db, Func<ApplicationDbContext, RecognitionService> service)
    {
        await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "collector" };
        var other = new ApplicationUser { Id = Guid.NewGuid(), UserName = "other" };
        var museum = new Museum { Name = "Collection museum" };
        db.AddRange(user, other, museum);
        await db.SaveChangesAsync();
        var a = new Artifact { MuseumId = museum.Id, Name = "A" };
        var b = new Artifact { MuseumId = museum.Id, Name = "B", IsActive = false };
        db.AddRange(a, b); await db.SaveChangesAsync();
        var image = new ArtifactMedia { ArtifactId = a.Id, SortOrder = 1, CreatedBy = user.Id };
        db.AddRange(image, new ArtifactMedia { ArtifactId = a.Id, SortOrder = 9, CreatedBy = user.Id });
        foreach (var artifact in new[] { a, a, b })
            db.Add(new RecognitionHistory { UserId = user.Id, ArtifactId = artifact.Id, IsSuccess = true, Method = "qr", RequestId = Guid.NewGuid(), RecognizedAt = DateTime.UtcNow });
        db.Add(new RecognitionHistory { UserId = other.Id, ArtifactId = a.Id, IsSuccess = false, RequestId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var collection = await service(db).GetCollectionAsync(user.Id, null, 1, 4);
        if (collection.TotalCollected != 2 || collection.Items.Count != 2 || collection.Museums.Count != 1)
            throw new Exception("Collection must deduplicate historical scans.");
        if (collection.Items.Single(x => x.ArtifactId == b.Id).Available)
            throw new Exception("Inactive artifacts must remain unavailable.");
        if (collection.Items.Single(x => x.ArtifactId == a.Id).ImageUrl != $"/api/artifacts/{a.Id}/media/{image.Id}/file")
            throw new Exception("Collection must use the first image in sort order.");
        if ((await service(db).GetCollectionAsync(other.Id, null, 1, 4)).TotalCollected != 0)
            throw new Exception("Collection leaked another user's or failed recognition data.");
        if ((await service(db).GetCollectionAsync(user.Id, -1, 1, 4)).TotalCount != 0)
            throw new Exception("Museum filter failed.");
        if ((await service(db).GetCollectionAsync(user.Id, null, 2, 1)).Items.Count != 1)
            throw new Exception("Collection pagination failed.");
        museum.IsActive = false; await db.SaveChangesAsync();
        if ((await service(db).GetCollectionAsync(user.Id, null, 1, 4)).Items.Any(x => x.Available))
            throw new Exception("Inactive museum exposed available collection items.");
        Console.WriteLine("Collection database checks passed.");
    }
}
