using SMGS.Application.DTOs.IndoorMap;

namespace SMGS.Application.Interfaces;

public interface IIndoorMapService
{
    Task<IReadOnlyList<MuseumOption>> Museums(Guid user, bool admin);
    Task<StructureResult> Structure(int museumId, Guid user, bool admin);
    Task SetLayout(int museumId, string layout, Guid user, bool admin);
    Task<int> AddBuilding(int museumId, BuildingRequest request, Guid user, bool admin);
    Task<int> AddFloor(int museumId, FloorRequest request, Guid user, bool admin);
    Task Assign(int museumId, Guid staffUserId);
    Task<IReadOnlyList<MapSummary>> Maps(int museumId, Guid user, bool admin);
    Task<int> Create(int museumId, CreateMapRequest request, Guid user, bool admin);
    Task<MapDocument> Get(int id, Guid user, bool admin);
    Task<object> Links(int id, Guid user, bool admin);
    Task<MapDocument> Save(int id, MapDraft draft, Guid user, bool admin);
    Task<MapDocument> SetImage(int id, int revision, string key, int width, int height, bool confirm, Guid user, bool admin);
    Task<MapDocument> Publish(int id, int revision, bool publish, Guid user, bool admin);
    Task<RouteResult> Route(int id, RouteRequest request, Guid user, bool admin);
    Task<MapDocument> Published(int id);
    Task<string> ImageKey(int id, Guid user, bool admin, bool published);
    Task<int> QrArtifactId(int id, Guid markerId, Guid user, bool admin);
}
