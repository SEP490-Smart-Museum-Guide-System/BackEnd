namespace SMGS.Domain.Entities;

public class AdmissionTicket
{
    public Guid OrderId { get; set; }
    public string Code { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal RefundedAmount { get; set; }
}

public class AdmissionCheckIn
{
    public Guid OrderId { get; set; }
    public Guid OperationId { get; set; }
    public Guid StaffId { get; set; }
    public int Quantity { get; set; }
    public DateTime CheckedInAt { get; set; }
}
