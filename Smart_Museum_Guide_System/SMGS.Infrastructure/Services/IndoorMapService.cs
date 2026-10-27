using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Application.Interfaces;
using SMGS.Application.Services;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class IndoorMapService(ApplicationDbContext db) : IIndoorMapService
{
    private IQueryable<int> Assigned(Guid user) => db.StaffAssignments
        .Where(a => a.UserId == user && a.GalleryId == null && a.ArtifactId == null).Select(a => a.MuseumId);

    private async Task Access(int museumId, Guid user, bool admin)
    {
        if (!admin && !await Assigned(user).ContainsAsync(museumId))
            throw new IndoorMapException("Bạn không được phân công quản lý bảo tàng này.", 403);
        if (!await db.Museums.AnyAsync(m => m.Id == museumId)) throw new IndoorMapException("Không tìm thấy bảo tàng.", 404);
    }

    private async Task<IndoorMap> Load(int id, Guid user, bool admin, bool graph = true)
    {
        var query = db.IndoorMaps.AsQueryable();
        if (graph) query = query.Include(m => m.Nodes).Include(m => m.Markers).Include(m => m.Edges).AsSplitQuery();
        var map = await query.SingleOrDefaultAsync(m => m.Id == id) ?? throw new IndoorMapException("Không tìm thấy sơ đồ.", 404);
        await Access(map.MuseumId, user, admin);
        return map;
    }

    private static void CheckRevision(IndoorMap map, int revision)
    {
        if (map.Revision != revision) throw new IndoorMapException("Sơ đồ đã được người khác thay đổi. Hãy tải lại trước khi lưu.", 409);
    }

    private static MapDocument Document(IndoorMap m) => new()
    {
        Id = m.Id, MuseumId = m.MuseumId, FloorId = m.FloorId, Name = m.Name, Revision = m.Revision,
        ImageKey = m.ImageKey, ImageWidth = m.ImageWidth, ImageHeight = m.ImageHeight,
        PublishedAt = m.PublishedAt, Nodes = m.Nodes.ToList(), Markers = m.Markers.ToList(), Edges = m.Edges.ToList()
    };

    public async Task<IReadOnlyList<MuseumOption>> Museums(Guid user, bool admin) => await db.Museums
        .Where(m => admin || Assigned(user).Contains(m.Id)).OrderBy(m => m.Name).Select(m => new MuseumOption(m.Id, m.Name)).ToListAsync();

    public async Task<StructureResult> Structure(int museumId, Guid user, bool admin)
    {
        await Access(museumId, user, admin);
        var layout = await db.Museums.Where(m => m.Id == museumId).Select(m => m.IndoorLayout).SingleAsync();
        var buildings = await db.Buildings.Where(b => b.MuseumId == museumId).OrderBy(b => b.Name).ToListAsync();
        var floors = await (from f in db.Floors join b in db.Buildings on f.BuildingId equals b.Id
            where b.MuseumId == museumId orderby b.Name, f.LevelNo
            select new FloorOption(f.Id, b.Id, b.Name, f.LevelNo, f.Name)).ToListAsync();
        return new(layout, buildings, floors);
    }

    public async Task SetLayout(int museumId, string layout, Guid user, bool admin)
    {
        await Access(museumId, user, admin);
        if (layout is not ("single" or "floors")) throw new IndoorMapException("Hãy chọn mặt bằng chung hoặc phân tầng.");
        // Lock the museum while configuring structure so a concurrent create cannot bypass this check.
        await using var tx = await db.Database.BeginTransactionAsync();
        var museum = await db.Museums.FromSqlInterpolated($"SELECT * FROM museums WHERE \"Id\" = {museumId} FOR UPDATE").SingleAsync();
        if (museum.IndoorLayout != layout && (await db.IndoorMaps.AnyAsync(m => m.MuseumId == museumId)
            || await db.Buildings.AnyAsync(b => b.MuseumId == museumId)))
            throw new IndoorMapException("Đã có cấu trúc hoặc sơ đồ. Không thể đổi chế độ mặt bằng để tránh mất liên kết.", 409);
        museum.IndoorLayout = layout; await db.SaveChangesAsync(); await tx.CommitAsync();
    }

    public async Task<int> AddBuilding(int museumId, BuildingRequest request, Guid user, bool admin)
    {
        await Access(museumId, user, admin);
        await using var tx = await db.Database.BeginTransactionAsync();
        var museum = await db.Museums.FromSqlInterpolated($"SELECT * FROM museums WHERE \"Id\" = {museumId} FOR UPDATE").SingleAsync();
        if (museum.IndoorLayout != "floors") throw new IndoorMapException("Chỉ thêm tòa nhà khi bảo tàng được cấu hình theo tầng.");
        var building = new Building { MuseumId = museumId, Name = request.Name.Trim() };
        db.Buildings.Add(building); await db.SaveChangesAsync(); await tx.CommitAsync(); return building.Id;
    }

    public async Task<int> AddFloor(int museumId, FloorRequest request, Guid user, bool admin)
    {
        await Access(museumId, user, admin);
        if (!await db.Buildings.AnyAsync(b => b.Id == request.BuildingId && b.MuseumId == museumId))
            throw new IndoorMapException("Tòa nhà không thuộc bảo tàng.");
        if (await db.Floors.AnyAsync(f => f.BuildingId == request.BuildingId && f.LevelNo == request.LevelNo))
            throw new IndoorMapException("Số tầng đã tồn tại trong tòa nhà.", 409);
        var floor = new Floor { BuildingId = request.BuildingId, LevelNo = request.LevelNo, Name = request.Name.Trim() };
        db.Floors.Add(floor); await db.SaveChangesAsync(); return floor.Id;
    }

    public async Task Assign(int museumId, Guid staffUserId)
    {
        if (!await db.Museums.AnyAsync(m => m.Id == museumId)) throw new IndoorMapException("Không tìm thấy bảo tàng.", 404);
        if (!await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
            where ur.UserId == staffUserId && role.Name == RoleNames.MuseumStaff select ur).AnyAsync())
            throw new IndoorMapException("Tài khoản phải có vai trò nhân viên bảo tàng.");
        if (await Assigned(staffUserId).ContainsAsync(museumId)) return;
        db.StaffAssignments.Add(new StaffAssignment { UserId = staffUserId, MuseumId = museumId });
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<MapSummary>> Maps(int museumId, Guid user, bool admin)
    {
        await Access(museumId, user, admin);
        return await db.IndoorMaps.Where(m => m.MuseumId == museumId).OrderBy(m => m.Name)
            .Select(m => new MapSummary(m.Id, m.FloorId, m.Name, m.Revision, m.PublishedAt)).ToListAsync();
    }

    public async Task<int> Create(int museumId, CreateMapRequest request, Guid user, bool admin)
    {
        await Access(museumId, user, admin);
        await using var tx = await db.Database.BeginTransactionAsync();
        var museum = await db.Museums.FromSqlInterpolated($"SELECT * FROM museums WHERE \"Id\" = {museumId} FOR UPDATE").SingleAsync();
        if (museum.IndoorLayout == "unknown") throw new IndoorMapException("Hãy xác định cấu trúc bảo tàng trước khi tạo sơ đồ.");
        if ((museum.IndoorLayout == "single") != (request.FloorId is null)) throw new IndoorMapException("Tầng không phù hợp chế độ mặt bằng.");
        if (request.FloorId.HasValue && !await (from f in db.Floors join b in db.Buildings on f.BuildingId equals b.Id
                where f.Id == request.FloorId && b.MuseumId == museumId select f).AnyAsync())
            throw new IndoorMapException("Tầng không thuộc bảo tàng.");
        if (await db.IndoorMaps.AnyAsync(m => m.MuseumId == museumId && m.FloorId == request.FloorId))
            throw new IndoorMapException("Mặt bằng này đã có sơ đồ.", 409);
        var map = new IndoorMap { MuseumId = museumId, FloorId = request.FloorId, Name = request.Name.Trim() };
        db.IndoorMaps.Add(map); await db.SaveChangesAsync(); await tx.CommitAsync(); return map.Id;
    }

    public async Task<MapDocument> Get(int id, Guid user, bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var result = Document(await Load(id, user, admin));
        await tx.CommitAsync(); return result;
    }

    public async Task<object> Links(int id, Guid user, bool admin)
    {
        var map = await Load(id, user, admin, false);
        return new
        {
            Artifacts = await db.Artifacts.Where(a => a.MuseumId == map.MuseumId && a.IsActive)
                .OrderBy(a => a.Name).Select(a => new LinkOption(a.Id, a.Name)).ToListAsync(),
            Galleries = await (from g in db.Galleries join f in db.Floors on g.FloorId equals f.Id
                join b in db.Buildings on f.BuildingId equals b.Id
                where b.MuseumId == map.MuseumId && (map.FloorId == null || g.FloorId == map.FloorId)
                orderby g.Name select new LinkOption(g.Id, g.Name)).ToListAsync()
        };
    }

    private async Task ValidateLinks(IndoorMap map, MapDraft draft)
    {
        var artifactIds = draft.Markers.Where(m => m.ArtifactId.HasValue).Select(m => m.ArtifactId!.Value).Distinct().ToArray();
        var galleryIds = draft.Markers.Where(m => m.GalleryId.HasValue).Select(m => m.GalleryId!.Value).Distinct().ToArray();
        if (await db.Artifacts.CountAsync(a => artifactIds.Contains(a.Id) && a.MuseumId == map.MuseumId && a.IsActive) != artifactIds.Length)
            throw new IndoorMapException("Hiện vật không thuộc bảo tàng hoặc đã ngừng hoạt động.");
        var count = await (from g in db.Galleries join f in db.Floors on g.FloorId equals f.Id
            join b in db.Buildings on f.BuildingId equals b.Id
            where galleryIds.Contains(g.Id) && b.MuseumId == map.MuseumId && (map.FloorId == null || g.FloorId == map.FloorId) select g).CountAsync();
        if (count != galleryIds.Length) throw new IndoorMapException("Phòng không thuộc phạm vi sơ đồ.");
    }

    public async Task<MapDocument> Save(int id, MapDraft draft, Guid user, bool admin)
    {
        IndoorGraph.Validate(draft);
        await using var tx = await db.Database.BeginTransactionAsync();
        var map = await Load(id, user, admin, false); CheckRevision(map, draft.Revision);
        await ValidateLinks(map, draft);
        map.Revision++; await db.SaveChangesAsync(); // Claims the revision before replacing dependent rows.
        await db.MapMarkers.Where(m => m.IndoorMapId == id).ExecuteDeleteAsync();
        await db.MapEdges.Where(m => m.IndoorMapId == id).ExecuteDeleteAsync();
        await db.MapNodes.Where(m => m.IndoorMapId == id).ExecuteDeleteAsync();
        foreach (var n in draft.Nodes) n.IndoorMapId = id;
        foreach (var m in draft.Markers) m.IndoorMapId = id;
        foreach (var e in draft.Edges) e.IndoorMapId = id;
        db.MapNodes.AddRange(draft.Nodes); db.MapMarkers.AddRange(draft.Markers); db.MapEdges.AddRange(draft.Edges);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        map.Nodes = draft.Nodes; map.Markers = draft.Markers; map.Edges = draft.Edges;
        return Document(map);
    }

    public async Task<MapDocument> SetImage(int id, int revision, string key, int width, int height, bool confirm, Guid user, bool admin)
    {
        var map = await Load(id, user, admin); CheckRevision(map, revision);
        if (!confirm && map.ImageWidth > 0 && Math.Abs((double)width / height - (double)map.ImageWidth / map.ImageHeight) > 0.001)
            throw new IndoorMapException("Ảnh mới khác tỷ lệ. Hãy xác nhận vì các điểm có thể lệch vị trí.", 409);
        map.ImageKey = key; map.ImageWidth = width; map.ImageHeight = height; map.Revision++;
        await db.SaveChangesAsync(); return Document(map);
    }

    public async Task<MapDocument> Publish(int id, int revision, bool publish, Guid user, bool admin)
    {
        // Repeatable read keeps the image and graph in the same revision while snapshotting.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var map = await Load(id, user, admin); CheckRevision(map, revision);
        var document = Document(map);
        if (publish)
        {
            if (map.ImageKey is null) throw new IndoorMapException("Hãy tải sơ đồ trước khi xuất bản.");
            IndoorGraph.Validate(document, true); await ValidateLinks(map, document);
            map.PublishedAt = DateTime.UtcNow; document.PublishedAt = map.PublishedAt;
            map.PublishedJson = JsonSerializer.Serialize(document);
        }
        else { map.PublishedJson = null; map.PublishedAt = null; }
        map.Revision++; await db.SaveChangesAsync(); await tx.CommitAsync();
        var result = Document(map); result.Warnings = IndoorGraph.Warnings(result); return result;
    }

    public async Task<RouteResult> Route(int id, RouteRequest request, Guid user, bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var map = await Load(id, user, admin); CheckRevision(map, request.Revision);
        if (map.ImageKey is null) throw new IndoorMapException("Hãy tải sơ đồ trước khi thử đường đi.");
        var route = IndoorGraph.Route(Document(map), map.ImageWidth, map.ImageHeight, request.FromNodeId, request.ToNodeId, request.AccessibleOnly);
        await tx.CommitAsync(); return route;
    }

    public async Task<MapDocument> Published(int id)
    {
        var json = await (from map in db.IndoorMaps join museum in db.Museums on map.MuseumId equals museum.Id
            where map.Id == id && museum.IsActive select map.PublishedJson).SingleOrDefaultAsync();
        if (json is null) throw new IndoorMapException("Sơ đồ chưa được xuất bản.", 404);
        return JsonSerializer.Deserialize<MapDocument>(json)!;
    }

    public async Task<string> ImageKey(int id, Guid user, bool admin, bool published) =>
        (published ? (await Published(id)).ImageKey : (await Load(id, user, admin, false)).ImageKey)
        ?? throw new IndoorMapException("Sơ đồ chưa có ảnh.", 404);

    public async Task<int> QrArtifactId(int id, Guid markerId, Guid user, bool admin)
    {
        var map = await Load(id, user, admin);
        var marker = map.Markers.SingleOrDefault(m => m.Id == markerId && m.Type == "artifact" && m.IsActive && m.ArtifactId != null)
            ?? throw new IndoorMapException("Chỉ điểm hiện vật đang hoạt động mới có mã QR.", 404);
        if (!await db.Artifacts.AnyAsync(a => a.Id == marker.ArtifactId && a.MuseumId == map.MuseumId && a.IsActive))
            throw new IndoorMapException("Hiện vật không thuộc bảo tàng hoặc đã ngừng hoạt động.", 404);
        return marker.ArtifactId!.Value;
    }
}
