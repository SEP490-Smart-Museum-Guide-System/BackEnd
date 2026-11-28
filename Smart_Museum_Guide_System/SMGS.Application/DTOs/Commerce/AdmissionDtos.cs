using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Commerce;

public class AdmissionOfferRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [Range(1, 999999999)] public int Price { get; set; }
    public bool IsActive { get; set; }
}

public class AdmissionOrderRequest : OrderRequest
{
    [Range(1, 1000)] public int Quantity { get; set; }
    [Range(1, int.MaxValue)] public int? DigitalProductId { get; set; }
}

public class AdmissionScanRequest
{
    [Required, RegularExpression("^[A-F0-9]{64}$")] public string Code { get; set; } = "";
}

public class AdmissionCheckInRequest : AdmissionScanRequest
{
    public Guid OperationId { get; set; }
    [Range(1, 1000)] public int Quantity { get; set; }
}

public record AdmissionOffer(int Id, int MuseumId, string Name, decimal Price, string Currency, bool IsActive);
public record AdmissionTicketResponse(Guid OrderId, int MuseumId, DateOnly VisitDate, string Code,
    int Quantity, int Used, int Remaining, bool CanCheckIn);
public record AdmissionCheckInResponse(Guid OperationId, int Quantity, DateTime CheckedInAt, AdmissionTicketResponse Ticket);
public record CheckoutDetails(Guid OrderId, decimal TicketAmount, decimal DigitalAmount, DateTime? TicketCancelledAt,
    DateTime? DigitalCancelledAt, decimal TicketRefundedAmount, decimal DigitalRefundedAmount);
