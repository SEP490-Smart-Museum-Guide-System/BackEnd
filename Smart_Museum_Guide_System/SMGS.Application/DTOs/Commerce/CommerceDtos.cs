using System.ComponentModel.DataAnnotations;
using SMGS.Domain.Entities;

namespace SMGS.Application.DTOs.Commerce;

public class ResourceRequest
{
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; set; } = "";
    [RegularExpression("^(admission|digital)$")] public string Kind { get; set; } = "digital";
    [RegularExpression("^(free|premium)$")] public string AccessLevel { get; set; } = "free";
    public int? ArtifactId { get; set; }
    [StringLength(200)] public string? ScopeReference { get; set; }
}

public class ProductRequest
{
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; set; } = "";
    [RegularExpression("^(admission|digital|combo)$")] public string Kind { get; set; } = "digital";
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal Price { get; set; }
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; set; } = "VND";
    [Range(1, 1000)] public int Seats { get; set; } = 1;
    [Range(1, 87600)] public int? ValidityHours { get; set; }
    [RegularExpression("^payment$")] public string? ValidityStartsAt { get; set; }
    [Required, MinLength(1), MaxLength(200)] public int[] ResourceIds { get; set; } = [];
    public bool IsActive { get; set; }
}

public class OrderRequest
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Required, StringLength(200, MinimumLength = 1)] public string BuyerName { get; set; } = "";
    // Business-defined invoice fields, preserved as a snapshot without claiming a legal e-invoice schema.
    public Dictionary<string, string> BuyerDetails { get; set; } = [];
}

public class ConfirmPaymentRequest
{
    public bool Succeeded { get; set; }
    [StringLength(200)] public string? Reference { get; set; }
    [StringLength(2000)] public string? FailureReason { get; set; }
}

public class RedeemKeyRequest
{
    [Required, StringLength(64, MinimumLength = 64)] public string Key { get; set; } = "";
}

public record ProductResponse(int Id, int MuseumId, string Name, string Kind, decimal Price,
    string Currency, int Seats, int? ValidityHours, string? ValidityStartsAt, int[] ResourceIds, bool IsActive);
public record OrderResponse(Guid Id, int MuseumId, string ProductName, string ProductKind, decimal Amount,
    string Currency, int Seats, int ValidityHours, string ValidityStartsAt, int[] ResourceIds, string BuyerName,
    Dictionary<string, string> BuyerDetails, string Status, DateTime CreatedAt, DateTime? PaidAt, DateTime? ExpiresAt);
public record GroupKeyResponse(string Key, int RemainingClaims);
public record RevenueResponse(string Currency, int PaidOrders, decimal Revenue, int FailedAttempts);
public record AccessResponse(bool Allowed, string AccessLevel);
public record PassResponse(Guid Id, Guid OrderId, int MuseumId, string ProductName, int[] ResourceIds,
    DateTime ClaimedAt, DateTime? ExpiresAt, DateTime? RevokedAt, bool IsValid);
public record ManagedOrderResponse(OrderResponse Order, IReadOnlyList<PaymentAttemptResponse> Transactions,
    PurchaseInvoice? Invoice);
public record PaymentAttemptResponse(Guid Id, string Status, string? Reference, string? FailureReason, DateTime CreatedAt, DateTime? CompletedAt);

public class CommerceException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
