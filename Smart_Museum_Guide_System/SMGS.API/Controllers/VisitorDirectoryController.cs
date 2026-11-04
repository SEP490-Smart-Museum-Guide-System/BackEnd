using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

[ApiController, Route("api/museums/{museumId:int}/visitor-directory"), Authorize]
public class VisitorDirectoryController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int museumId, CancellationToken token)
    {
        var museum = await db.Museums.AsNoTracking().Where(m => m.Id == museumId && m.IsActive)
            .Select(m => new { m.Id, m.Name }).SingleOrDefaultAsync(token);
        if (museum is null) return NotFound(ResponseDto<object>.Fail("Không tìm thấy bảo tàng.", 404));
        var rooms = await (from g in db.Galleries.AsNoTracking()
            join f in db.Floors on g.FloorId equals f.Id
            join b in db.Buildings on f.BuildingId equals b.Id
            where b.MuseumId == museumId
            orderby b.Name, f.LevelNo, g.Name
            select new { g.Id, g.Name, FloorId = f.Id, FloorName = f.Name, f.LevelNo, BuildingName = b.Name }).ToListAsync(token);
        var artifacts = await db.Artifacts.AsNoTracking().Where(a => a.MuseumId == museumId && a.IsActive)
            .OrderBy(a => a.Name).Select(a => new { a.Id, a.Name, a.GalleryId }).ToListAsync(token);
        var snapshots = await db.IndoorMaps.AsNoTracking().Where(m => m.MuseumId == museumId && m.PublishedJson != null)
            .Select(m => new { m.Id, m.PublishedJson }).ToListAsync(token);
        var maps = new List<MapDocument>();
        foreach (var snapshot in snapshots)
        {
            var map = JsonSerializer.Deserialize<MapDocument>(snapshot.PublishedJson!);
            if (map is not null && map.Id == snapshot.Id && map.MuseumId == museumId) maps.Add(map);
        }
        object MapInfo(MapDocument map)
        {
            var room = rooms.FirstOrDefault(g => g.FloorId == map.FloorId);
            return new { map.Id, map.Name, map.FloorId, map.Revision, FloorName = room?.FloorName, BuildingName = room?.BuildingName };
        }
        var rows = artifacts.Select(artifact =>
        {
            var room = rooms.FirstOrDefault(g => g.Id == artifact.GalleryId);
            var linked = maps.Where(map => VisitorMapCatalog.HasDestination(map, artifact.Id, artifact.GalleryId, room?.FloorId)).Select(MapInfo).ToList();
            return new { artifact.Id, artifact.Name, artifact.GalleryId, GalleryName = room?.Name,
                FloorId = room?.FloorId, FloorName = room?.FloorName, BuildingName = room?.BuildingName, Maps = linked };
        }).ToList();
        return Ok(ResponseDto<object>.Success(new { MuseumId = museum.Id, MuseumName = museum.Name, Galleries = rooms, Maps = maps.Select(MapInfo), Artifacts = rows }));
    }
}
