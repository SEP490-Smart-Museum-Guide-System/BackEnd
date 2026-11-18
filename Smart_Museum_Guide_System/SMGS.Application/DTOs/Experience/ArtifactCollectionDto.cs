namespace SMGS.Application.DTOs.Experience;

public record CollectionMuseum(int Id, string Name);
public record CollectedArtifact(int ArtifactId, string Name, int MuseumId, string MuseumName,
    bool Available, string? ImageUrl, DateTime FirstDiscoveredAt);
public record ArtifactCollectionDto(IReadOnlyList<CollectedArtifact> Items, int TotalCount,
    int TotalCollected, IReadOnlyList<CollectionMuseum> Museums, int Page, int TotalPages);
