namespace SMGS.Domain.Entities;

public class Notification
{
    public long Id { get; set; }
    public Guid RecipientId { get; set; }
    public int? MuseumId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class NotificationTypes
{
    public const string ContentSubmitted = "CONTENT_SUBMITTED";
    public const string ContentApproved = "CONTENT_APPROVED";
    public const string ContentRejected = "CONTENT_REJECTED";
    public const string TourSubmitted = "TOUR_SUBMITTED";
    public const string TourApproved = "TOUR_APPROVED";
    public const string TourRejected = "TOUR_REJECTED";
    public const string PaymentSucceeded = "PAYMENT_SUCCEEDED";
    public const string FeedbackResponded = "FEEDBACK_RESPONDED";
    public const string ComplaintCreated = "COMPLAINT_CREATED";
    public const string ComplaintStatusUpdated = "COMPLAINT_STATUS_UPDATED";
}

public static class NotificationReferenceTypes
{
    public const string ArtifactContent = "ArtifactContent";
    public const string TourVersion = "TourVersion";
    public const string PurchaseOrder = "PurchaseOrder";
    public const string VisitFeedback = "VisitFeedback";
    public const string Complaint = "Complaint";
}
