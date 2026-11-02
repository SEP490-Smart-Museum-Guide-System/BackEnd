using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Application.Interfaces;
using SMGS.Application.Services;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class VisitorNavigationService(ApplicationDbContext db, IVisitProgressService progress) : IVisitorNavigationService
{
    public async Task<VisitorLocation> LocateFromRecognitionAsync(Guid user, int visitId)
    {
        await ExperienceAccess.Visit(db, visitId, user, true);
        var last = await db.Set<RecognitionHistory>().Where(x => x.UserId == user && x.VisitId == visitId && x.IsSuccess && x.ArtifactId != null)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync() ?? throw new ExperienceException("Hãy quét QR để xác định vị trí.", 409);
        return new(last.Id, last.ArtifactId!.Value, last.RecognizedAt);
    }
    public async Task<VisitorRoute> RouteToArtifactAsync(Guid user, int visitId, int mapId, int? artifactId, bool accessibleOnly)
    {
        var visit = await ExperienceAccess.Visit(db, visitId, user, true);
        var location = await LocateFromRecognitionAsync(user, visitId);
        var target = artifactId ?? (await progress.GetProgressAsync(user, visitId)).NextArtifactId
            ?? throw new ExperienceException("Hãy chọn hiện vật đích; tour không còn điểm tiếp theo.", 409);
        if (!await db.Artifacts.AnyAsync(a => a.Id == target && a.MuseumId == visit.MuseumId && a.IsActive) ||
            !await db.Artifacts.AnyAsync(a => a.Id == location.ArtifactId && a.IsActive)) throw new ExperienceException("Hiện vật không khả dụng.", 404);
        var json = await db.IndoorMaps.Where(m => m.Id == mapId && m.MuseumId == visit.MuseumId && db.Museums.Any(a => a.Id == m.MuseumId && a.IsActive))
            .Select(m => m.PublishedJson).SingleOrDefaultAsync();
        if (json is null) throw new ExperienceException("Chưa có sơ đồ được xuất bản.", 404);
        var map = JsonSerializer.Deserialize<MapDocument>(json) ?? throw new ExperienceException("Sơ đồ không hợp lệ.", 409);
        var starts = map.Markers.Where(m => m.IsActive && m.ArtifactId == location.ArtifactId && m.NodeId != null).ToList();
        var ends = map.Markers.Where(m => m.IsActive && m.ArtifactId == target && m.NodeId != null).ToList();
        if (starts.Count != 1 || ends.Count != 1) throw new ExperienceException("Hai hiện vật phải có vị trí duy nhất trên cùng sơ đồ đã xuất bản.", 422);
        return new(mapId, map.Revision, location.ArtifactId, target, IndoorGraph.Route(map, map.ImageWidth, map.ImageHeight, starts[0].NodeId!.Value, ends[0].NodeId!.Value, accessibleOnly));
    }
}
