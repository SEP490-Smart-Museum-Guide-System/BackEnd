namespace SMGS.Domain.Entities;

public class Building
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public string Name { get; set; } = "";
}

public class StaffAssignment
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public int MuseumId { get; set; }
    public int? GalleryId { get; set; }
    public int? ArtifactId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

public class IndoorMap
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public int? FloorId { get; set; }
    public string Name { get; set; } = "";
    public int Revision { get; set; } = 1;
    public string? ImageKey { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string? PublishedJson { get; set; }
    public DateTime? PublishedAt { get; set; }
    public ICollection<MapNode> Nodes { get; set; } = new List<MapNode>();
    public ICollection<MapMarker> Markers { get; set; } = new List<MapMarker>();
    public ICollection<MapEdge> Edges { get; set; } = new List<MapEdge>();
}

public class MapNode
{
    public Guid Id { get; set; }
    public int IndoorMapId { get; set; }
    public string Label { get; set; } = "";
    public string Type { get; set; } = "corridor";
    public double X { get; set; }
    public double Y { get; set; }
}

public class MapMarker
{
    public Guid Id { get; set; }
    public int IndoorMapId { get; set; }
    public string Label { get; set; } = "";
    public string Type { get; set; } = "artifact";
    public double X { get; set; }
    public double Y { get; set; }
    public int? ArtifactId { get; set; }
    public int? GalleryId { get; set; }
    public Guid? NodeId { get; set; }
    public bool IsDestination { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MapEdge
{
    public Guid Id { get; set; }
    public int IndoorMapId { get; set; }
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public bool Bidirectional { get; set; } = true;
    public bool IsAccessible { get; set; } = true;
}
