using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Experience;

public record PersonalizedTourRequest([Range(1, int.MaxValue)] int MuseumId,
    [Range(5, 240)] int AvailableMinutes, [Required, MinLength(1), MaxLength(8)] string[] Interests,
    [Range(1, 30)] int MaxStops = 12);
public record SavePersonalizedTourRequest(Guid RecommendationId);
public record PersonalTourCandidate(int ArtifactId, int ContentVersionId, int MuseumId, string Name, string Title,
    string Description, string[] Topics, int? GalleryId, string GalleryName, int? FloorId, double VisitMinutes);
public record TourModelRequest(int MuseumId, int AvailableMinutes, string[] Interests, int MaxStops,
    IReadOnlyList<PersonalTourCandidate> Candidates);
public record TourModelStop(int ArtifactId, int ContentVersionId, double VisitMinutes, double WalkMinutes,
    double Score, string[] MatchedInterests, string Reason);
public record TourModelResult(string EngineVersion, double EstimatedMinutes, bool UsedFallback, string Notice,
    IReadOnlyList<TourModelStop> Stops);
public record PersonalTourStop(int ArtifactId, int ContentVersionId, string Name, int OrderNo, int? GalleryId,
    string GalleryName, double VisitMinutes, double WalkMinutes, string Reason, string[] MatchedInterests);
public record PersonalTourResult(Guid Id, int MuseumId, string MuseumName, string[] Interests,
    int AvailableMinutes, double EstimatedMinutes, string EngineVersion, bool UsedFallback, string Notice,
    IReadOnlyList<PersonalTourStop> Stops, DateTime CreatedAt, bool IsSaved);
public record TourInterest(int Id, string Name);
