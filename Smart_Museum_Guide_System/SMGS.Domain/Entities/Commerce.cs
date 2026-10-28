namespace SMGS.Domain.Entities;

public class CommerceResource
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "digital";
    public string AccessLevel { get; set; } = "free";
    public int? ArtifactId { get; set; }
    public string? ScopeReference { get; set; }
}

public class CommerceProduct
{
    public int Id { get; set; }
    public int MuseumId { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "digital";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public int Seats { get; set; } = 1;
    // Null means the validity policy has not been confirmed: this product cannot be purchased yet.
    public int? ValidityHours { get; set; }
    public string? ValidityStartsAt { get; set; }
    public string ResourceIdsJson { get; set; } = "[]";
    public bool IsActive { get; set; }
}

public class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BuyerId { get; set; }
    public int MuseumId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductKind { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public int Seats { get; set; }
    public int ValidityHours { get; set; }
    public string ValidityStartsAt { get; set; } = "";
    public string ResourceIdsJson { get; set; } = "[]";
    public string BuyerName { get; set; } = "";
    public string BuyerDetailsJson { get; set; } = "{}";
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? GroupKeyHash { get; set; }
}

public class PaymentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "pending";
    public string? Reference { get; set; }
    public string? FailureReason { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class MuseumPass
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}

public class PurchaseInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Number { get; set; } = "";
    public string BuyerName { get; set; } = "";
    public string BuyerDetailsJson { get; set; } = "{}";
    public string ProductName { get; set; } = "";
    public string ProductKind { get; set; } = "";
    public int Seats { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
