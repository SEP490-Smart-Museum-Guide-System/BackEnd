namespace SMGS.Domain.Entities;

// Private visitor itinerary, not a published museum tour or admission product.
public class PersonalizedTour
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public int MuseumId { get; set; }
    public string SnapshotJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
