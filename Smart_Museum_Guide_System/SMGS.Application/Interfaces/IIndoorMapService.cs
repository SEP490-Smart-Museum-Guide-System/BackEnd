using SMGS.Application.DTOs.IndoorMap;
using SMGS.Domain.Entities;

namespace SMGS.Application.Interfaces;

public interface IIndoorMapService
{
    Task<IReadOnlyList<MuseumOption>> Museums(Guid user, bool admin);
    Task<StructureResult> Structure(int museumId, Guid user, bool admin);
    Task SetLayout(int museumId, string layout, Guid user, bool admin);
    Task<int> AddBuilding(int museumId, BuildingRequest request, Guid user, bool admin);
    Task<Building> UpdateBuilding(int museumId, int buildingId, BuildingRequest request, Guid user, bool admin);
    Task DeleteBuilding(int museumId, int buildingId, Guid user, bool admin);
    Task<int> AddFloor(int museumId, FloorRequest request, Guid user, bool admin);
    Task<FloorOption> UpdateFloor(int museumId, int floorId, FloorRequest request, Guid user, bool admin);
    Task DeleteFloor(int museumId, int floorId, Guid user, bool admin);
    Task<IReadOnlyList<GalleryOption>> Galleries(int museumId, int? floorId, Guid user, bool admin);
    Task<int> AddGallery(int museumId, GalleryRequest request, Guid user, bool admin);
    Task<GalleryOption> UpdateGallery(int museumId, int galleryId, GalleryRequest request, Guid user, bool admin);
    Task DeleteGallery(int museumId, int galleryId, Guid user, bool admin);
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
