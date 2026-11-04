using SMGS.Application.DTOs.IndoorMap;

namespace SMGS.Infrastructure.Services;

public static class VisitorMapCatalog
{
    public static bool HasDestination(MapDocument map, int artifactId, int? galleryId, int? floorId) =>
        (!map.FloorId.HasValue || map.FloorId == floorId) &&
        map.Markers.Count(marker => marker.IsActive && marker.IsDestination && marker.ArtifactId == artifactId &&
            marker.NodeId.HasValue && map.Nodes.Any(node => node.Id == marker.NodeId) &&
            (!marker.GalleryId.HasValue || marker.GalleryId == galleryId)) == 1;
}
