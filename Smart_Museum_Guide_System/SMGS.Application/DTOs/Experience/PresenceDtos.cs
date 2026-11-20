using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Experience;

public sealed record GpsAssessmentRequest(
    [Range(-90, 90)] double Latitude, [Range(-180, 180)] double Longitude,
    [Range(0, 100000)] double AccuracyMeters, DateTimeOffset CapturedAt,
    bool IsLastKnown = false, bool IsMocked = false);
public sealed record CheckpointScanRequest([Required, StringLength(250)] string Code, Guid RequestId, int? VisitId);
public sealed record PresenceRouteRequest(int MapId, Guid? TargetNodeId, int? ArtifactId, int? VisitId, bool AccessibleOnly = false);
public sealed record GpsAssessment(string Status, double? DistanceMeters, double AccuracyMeters, double RadiusMeters, string Message);
public sealed record MuseumPresenceResult(int MuseumId, string Status, string Source, DateTime? ConfirmedAt,
    bool IsFresh, string? Label, int? GalleryId, string? GalleryName, int? FloorId, string? FloorName,
    int? MapId, int? MapRevision, Guid? MarkerId, Guid? NodeId, double? X, double? Y, int? ArtifactId);
