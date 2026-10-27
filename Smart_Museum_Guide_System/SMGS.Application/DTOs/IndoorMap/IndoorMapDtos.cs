using System.ComponentModel.DataAnnotations;
using SMGS.Domain.Entities;

namespace SMGS.Application.DTOs.IndoorMap;

public record RevisionRequest([Range(1, int.MaxValue)] int Revision);
public record LayoutRequest([Required] string Layout);
public record BuildingRequest([Required, StringLength(200)] string Name);
public record FloorRequest(int BuildingId, int LevelNo, [Required, StringLength(200)] string Name);
public record CreateMapRequest(int? FloorId, [Required, StringLength(200)] string Name);
public record AssignmentRequest(Guid UserId);
public record RouteRequest(int Revision, Guid FromNodeId, Guid ToNodeId, bool AccessibleOnly = false);
public record RouteResult(IReadOnlyList<Guid> NodeIds, double Cost);
public record MuseumOption(int Id, string Name);
public record FloorOption(int Id, int BuildingId, string BuildingName, int LevelNo, string? Name);
public record LinkOption(int Id, string Name);
public record StructureResult(string Layout, IReadOnlyList<Building> Buildings, IReadOnlyList<FloorOption> Floors);
public record MapSummary(int Id, int? FloorId, string Name, int Revision, DateTime? PublishedAt);
public class MapDraft
{
    public int Revision { get; set; }
    public List<MapNode> Nodes { get; set; } = [];
    public List<MapMarker> Markers { get; set; } = [];
    public List<MapEdge> Edges { get; set; } = [];
}
public class MapDocument : MapDraft
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public int? FloorId { get; set; }
    public string Name { get; set; } = "";
    public string? ImageKey { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public DateTime? PublishedAt { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = [];
}
public class IndoorMapException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
