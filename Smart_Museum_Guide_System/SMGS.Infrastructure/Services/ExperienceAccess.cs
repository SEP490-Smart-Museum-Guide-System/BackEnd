using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

internal static class ExperienceAccess
{
    // Serialize mutations for a visitor across requests and API instances, inside the caller's transaction.
    public static async Task LockUser(ApplicationDbContext db, Guid user)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("A transaction is required.");
        if (await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {user} FOR UPDATE").SingleOrDefaultAsync() is null)
            throw new ExperienceException("Tài khoản không tồn tại.", 401);
    }
    public static async Task Museum(ApplicationDbContext db, int museum, Guid user, bool admin)
    {
        if (!await db.Museums.AnyAsync(x => x.Id == museum && x.IsActive)) throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
        if (!admin && !await db.StaffAssignments.AnyAsync(x => x.UserId == user && x.MuseumId == museum && x.ArtifactId == null && x.GalleryId == null))
            throw new ExperienceException("Bạn không được phân công bảo tàng này.", 403);
    }
    public static async Task<Artifact> Artifact(ApplicationDbContext db, int id, Guid user, bool admin)
    {
        var artifact = await db.Artifacts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.Museum!.IsActive)
            ?? throw new ExperienceException("Không tìm thấy hiện vật.", 404);
        if (!admin && !await db.StaffAssignments.AnyAsync(x => x.UserId == user && x.MuseumId == artifact.MuseumId &&
            (x.ArtifactId == id || (x.ArtifactId == null && (x.GalleryId == null || x.GalleryId == artifact.GalleryId)))))
            throw new ExperienceException("Bạn không được phân công hiện vật này.", 403);
        return artifact;
    }
    public static async Task<Visit> Visit(ApplicationDbContext db, int id, Guid user, bool active = false)
    {
        var visit = await db.Visits.SingleOrDefaultAsync(x => x.Id == id && x.VisitorId == user)
            ?? throw new ExperienceException("Không tìm thấy lượt tham quan.", 404);
        if (active && visit.Status != VisitStatus.InProgress) throw new ExperienceException("Lượt tham quan đã kết thúc.", 409);
        return visit;
    }
    public static (int Skip, int Take) Page(int page, int size) => ((Math.Clamp(page, 1, 100000) - 1) * Math.Clamp(size, 1, 100), Math.Clamp(size, 1, 100));
}
