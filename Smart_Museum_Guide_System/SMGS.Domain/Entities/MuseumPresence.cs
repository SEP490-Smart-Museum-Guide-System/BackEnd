namespace SMGS.Domain.Entities;

// A QR observation, not continuous tracking or proof against a copied QR/GPS spoofing.
public sealed class MuseumPresence
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int MuseumId { get; set; }
    public int? VisitId { get; set; }
    public int MapId { get; set; }
    public int MapRevision { get; set; }
    public Guid MarkerId { get; set; }
    public Guid RequestId { get; set; }
    public DateTime ObservedAt { get; set; }
}
