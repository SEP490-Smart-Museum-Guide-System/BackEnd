using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Application.Services;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public sealed class MuseumPresenceService(ApplicationDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan LocationLifetime = TimeSpan.FromMinutes(5);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private async Task<Museum> Museum(int id) => await db.Museums.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id && m.IsActive)
        ?? throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
    private async Task<DateTime?> CheckVisit(Guid user, int museumId, int? visitId)
    {
        if (visitId is not int id) return null;
        var visit = await ExperienceAccess.Visit(db, id, user, true);
        if (visit.MuseumId != museumId) throw new ExperienceException("Lượt tham quan không thuộc bảo tàng này.", 409);
        return visit.StartedAt;
    }
    private async Task<MapDocument> Map(int museumId, int mapId)
    {
        var json = await db.IndoorMaps.AsNoTracking().Where(m => m.Id == mapId && m.MuseumId == museumId)
            .Select(m => m.PublishedJson).SingleOrDefaultAsync();
        if (json is null) throw new ExperienceException("Sơ đồ chưa được xuất bản.", 404);
        var map = JsonSerializer.Deserialize<MapDocument>(json);
        if (map == null || map.Id != mapId || map.MuseumId != museumId) throw new ExperienceException("Sơ đồ không hợp lệ.", 409);
        return map;
    }
    private static MapMarker Checkpoint(MapDocument map, Guid id)
    {
        var marker = map.Markers.SingleOrDefault(m => m.Id == id && m.IsActive && m.NodeId != null
            && (m.Type == "entrance" || m.Type == "gallery") && map.Nodes.Any(n => n.Id == m.NodeId));
        return marker ?? throw new ExperienceException("Điểm QR cổng/phòng không còn khả dụng trên sơ đồ.", 404);
    }
    public async Task<string> QrText(int museumId, int mapId, Guid markerId, Guid user, bool admin)
    {
        await ExperienceAccess.Museum(db, museumId, user, admin);
        var map = await Map(museumId, mapId);
        Checkpoint(map, markerId);
        return $"smgs:location:v1:{mapId}:{map.Revision}:{markerId:N}";
    }
    public async Task<GpsAssessment> AssessGps(int museumId, GpsAssessmentRequest r)
    {
        var museum = await Museum(museumId);
        const double radius = 150;
        if (!double.IsFinite(r.Latitude) || !double.IsFinite(r.Longitude) || !double.IsFinite(r.AccuracyMeters)
            || Math.Abs(r.Latitude) > 90 || Math.Abs(r.Longitude) > 180 || r.AccuracyMeters < 0)
            throw new ExperienceException("Dữ liệu GPS không hợp lệ.");
        if (museum.Latitude == null || museum.Longitude == null || Math.Abs(museum.Latitude.Value) > 90 || Math.Abs(museum.Longitude.Value) > 180)
            return new("unavailable", null, r.AccuracyMeters, radius, "Bảo tàng chưa có tọa độ; hãy quét QR tại cổng hoặc phòng.");
        var age = clock.GetUtcNow() - r.CapturedAt;
        if (r.IsLastKnown || r.IsMocked || age < TimeSpan.FromSeconds(-5) || age > TimeSpan.FromSeconds(30) || r.AccuracyMeters > 50)
            return new("uncertain", null, r.AccuracyMeters, radius, "GPS cũ hoặc sai số lớn; quét QR để xác nhận đã đến.");
        double Rad(double v) => v * Math.PI / 180;
        var lat = Rad(r.Latitude); var target = Rad((double)museum.Latitude);
        var a = Math.Pow(Math.Sin((lat - target) / 2), 2) + Math.Cos(lat) * Math.Cos(target)
            * Math.Pow(Math.Sin(Rad(r.Longitude - (double)museum.Longitude) / 2), 2);
        var distance = 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
        var status = distance + r.AccuracyMeters <= radius ? "nearby" : distance - r.AccuracyMeters > radius ? "outside" : "uncertain";
        return new(status, distance, r.AccuracyMeters, radius, status switch
        {
            "nearby" => "Bạn đang ở gần bảo tàng. Quét QR tại cổng/phòng để xác nhận.",
            "outside" => "Bạn chưa ở gần bảo tàng theo mẫu GPS này.",
            _ => "Bạn đang ở rìa vùng định vị; GPS chưa đủ để xác nhận."
        });
    }
    public async Task<MuseumPresenceResult> Scan(int museumId, Guid user, CheckpointScanRequest r)
    {
        await Museum(museumId);
        await CheckVisit(user, museumId, r.VisitId);
        var parts = r.Code?.Split(':');
        if (r.RequestId == Guid.Empty || parts is not { Length: 6 } || parts[0] != "smgs" || parts[1] != "location" || parts[2] != "v1"
            || !int.TryParse(parts[3], out var mapId) || !int.TryParse(parts[4], out var revision) || !Guid.TryParse(parts[5], out var markerId))
            throw new ExperienceException("Mã QR vị trí không hợp lệ.");
        var map = await Map(museumId, mapId);
        if (revision != map.Revision) throw new ExperienceException("QR thuộc sơ đồ cũ. Vui lòng dùng QR đang được bảo tàng niêm yết.", 409);
        Checkpoint(map, markerId);
        await using var tx = await db.Database.BeginTransactionAsync();
        await ExperienceAccess.LockUser(db, user);
        // Hold the published snapshot stable until the observation commits.
        var locked = await db.IndoorMaps.FromSqlInterpolated($"SELECT * FROM indoor_maps WHERE \"Id\" = {mapId} AND \"MuseumId\" = {museumId} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync();
        if (locked?.PublishedJson == null) throw new ExperienceException("Sơ đồ chưa được xuất bản.", 404);
        var lockedMap = JsonSerializer.Deserialize<MapDocument>(locked.PublishedJson);
        if (lockedMap == null || lockedMap.Id != mapId || lockedMap.MuseumId != museumId || lockedMap.Revision != revision)
            throw new ExperienceException("QR thuộc sơ đồ cũ. Vui lòng quét lại QR đang niêm yết.", 409);
        Checkpoint(lockedMap, markerId);
        var existing = await db.Set<MuseumPresence>().SingleOrDefaultAsync(x => x.UserId == user && x.RequestId == r.RequestId);
        if (existing != null && (existing.MuseumId != museumId || existing.MapId != mapId || existing.MapRevision != revision
            || existing.MarkerId != markerId || existing.VisitId != r.VisitId))
            throw new ExperienceException("Yêu cầu đã được dùng cho điểm xác nhận khác.", 409);
        if (existing == null)
        {
            db.Add(new MuseumPresence { MuseumId = museumId, UserId = user, VisitId = r.VisitId, MapId = mapId,
                MapRevision = revision, MarkerId = markerId, RequestId = r.RequestId, ObservedAt = Now });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return await Current(museumId, user, r.VisitId);
    }
    public async Task<MuseumPresenceResult> Current(int museumId, Guid user, int? visitId, int? preferredMapId = null)
    {
        return await CurrentCore(museumId, user, visitId, preferredMapId);
    }
    public async Task<MuseumPresenceResult> ScanAny(Guid user, CheckpointScanRequest r)
    {
        var parts = r.Code?.Split(':');
        if (parts is not { Length: 6 } || !int.TryParse(parts[3], out var mapId)) throw new ExperienceException("Mã QR vị trí không hợp lệ.");
        var museumId = await db.IndoorMaps.Where(m => m.Id == mapId).Select(m => (int?)m.MuseumId).SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy điểm QR.", 404);
        return await Scan(museumId, user, r);
    }
    private async Task<MuseumPresenceResult> CurrentCore(int museumId, Guid user, int? visitId, int? preferredMapId)
    {
        await Museum(museumId);
        var visitStartedAt = await CheckVisit(user, museumId, visitId);
        var checkpoint = await db.Set<MuseumPresence>().AsNoTracking().Where(p => p.UserId == user && p.MuseumId == museumId
            && (visitId == null || p.VisitId == null || p.VisitId == visitId)).OrderByDescending(p => p.ObservedAt).ThenByDescending(p => p.Id).FirstOrDefaultAsync();
        var snapshots = await db.IndoorMaps.AsNoTracking()
            .Where(m => m.MuseumId == museumId && m.PublishedJson != null)
            .Select(m => m.PublishedJson!).ToListAsync();
        var anchors = snapshots.Select(s => JsonSerializer.Deserialize<MapDocument>(s)!)
            .SelectMany(m => m.Markers.Where(p => p.IsActive && p.ArtifactId != null &&
                p.NodeId != null && m.Nodes.Any(n => n.Id == p.NodeId))
                .Select(p => (Map: m, Marker: p)))
            .GroupBy(p => p.Marker.ArtifactId!.Value)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        var anchoredIds = anchors.Keys.ToArray();
        var galleryIds = from g in db.Galleries join f in db.Floors on g.FloorId equals f.Id
            join b in db.Buildings on f.BuildingId equals b.Id where b.MuseumId == museumId select g.Id;
        var recognition = await (from r in db.Set<RecognitionHistory>().AsNoTracking()
            join a in db.Artifacts on r.ArtifactId equals a.Id
            where r.UserId == user && r.IsSuccess && r.ConfirmedQrScan && a.IsActive && a.MuseumId == museumId
                && (anchoredIds.Contains(a.Id) || (a.GalleryId.HasValue && galleryIds.Contains(a.GalleryId.Value)))
                && (visitId == null || r.VisitId == visitId ||
                    (r.VisitId == null && r.RecognizedAt >= visitStartedAt))
            orderby r.RecognizedAt descending, r.Id descending
            select new { r.RecognizedAt, Artifact = a }).FirstOrDefaultAsync();
        var useCheckpoint = checkpoint != null && (recognition == null || checkpoint.ObservedAt >= recognition.RecognizedAt);
        var at = useCheckpoint ? checkpoint!.ObservedAt : recognition?.RecognizedAt;
        var artifact = useCheckpoint ? null : recognition?.Artifact;
        MapDocument? map = null; MapMarker? marker = null;
        if (useCheckpoint)
        {
            try
            {
                map = await Map(museumId, checkpoint!.MapId);
                if (map.Revision == checkpoint.MapRevision) marker = Checkpoint(map, checkpoint.MarkerId);
            }
            catch (ExperienceException) { /* Preserve last observation but never route against a removed anchor. */ }
        }
        else if (artifact != null && anchors.TryGetValue(artifact.Id, out var anchor))
        {
            (map, marker) = anchor;
        }
        var galleryId = marker?.GalleryId ?? artifact?.GalleryId;
        var room = await (from g in db.Galleries.AsNoTracking() join f in db.Floors on g.FloorId equals f.Id
            join b in db.Buildings on f.BuildingId equals b.Id where g.Id == galleryId && b.MuseumId == museumId
            select new { g.Name, FloorId = f.Id, FloorName = f.Name, BuildingName = b.Name }).FirstOrDefaultAsync();
        var floorId = room?.FloorId ?? map?.FloorId;
        var floor = room == null && floorId != null
            ? await (from f in db.Floors join b in db.Buildings on f.BuildingId equals b.Id
                where f.Id == floorId && b.MuseumId == museumId
                select new { FloorName = f.Name, BuildingName = b.Name }).FirstOrDefaultAsync()
            : null;
        var floorName = room?.FloorName ?? floor?.FloorName;
        var fresh = at.HasValue && at <= Now && Now - at.Value <= LocationLifetime && (!useCheckpoint || marker != null);
        return new(museumId, at == null ? "unknown" : fresh ? "confirmed" : "last_known", at == null ? "none" : useCheckpoint ? "checkpoint_qr" : "artifact_qr",
            at, fresh, marker?.Label ?? artifact?.Name, galleryId, room?.Name, floorId, floorName, map?.Id, map?.Revision,
            marker?.Id, marker?.NodeId, marker?.X, marker?.Y, artifact?.Id, room?.BuildingName ?? floor?.BuildingName);
    }
    public async Task<object> Route(int museumId, Guid user, PresenceRouteRequest r)
    {
        var current = await Current(museumId, user, r.VisitId, r.MapId);
        if (!current.IsFresh || current.NodeId == null) throw new ExperienceException("Vị trí chưa xác định hoặc đã cũ. Hãy quét QR tại vị trí đang đứng.", 409);
        if (current.MapId != r.MapId) throw new ExperienceException("Bạn đã chuyển tầng/phòng. Hãy mở sơ đồ đúng vị trí vừa quét.", 422);
        var map = await Map(museumId, r.MapId);
        if (map.Revision != current.MapRevision) throw new ExperienceException("Sơ đồ vừa thay đổi. Hãy tải lại và quét QR mới.", 409);
        var target = r.TargetNodeId;
        if (r.ArtifactId is int aid)
        {
            if (!await db.Artifacts.AnyAsync(a => a.Id == aid && a.MuseumId == museumId && a.IsActive)) throw new ExperienceException("Hiện vật không khả dụng.", 404);
            var markers = map.Markers.Where(m => m.IsActive && m.IsDestination && m.ArtifactId == aid && m.NodeId != null).ToList();
            if (markers.Count != 1) throw new ExperienceException("Hiện vật chưa có vị trí duy nhất trên sơ đồ.", 422);
            target = markers[0].NodeId;
        }
        if (target == null) throw new ExperienceException("Hãy chọn điểm đến.");
        try { return new { MapId = map.Id, map.Revision, FromNodeId = current.NodeId, Route = IndoorGraph.Route(map, map.ImageWidth, map.ImageHeight, current.NodeId.Value, target.Value, r.AccessibleOnly) }; }
        catch (IndoorMapException e) { throw new ExperienceException(e.Message, e.StatusCode); }
    }
}


