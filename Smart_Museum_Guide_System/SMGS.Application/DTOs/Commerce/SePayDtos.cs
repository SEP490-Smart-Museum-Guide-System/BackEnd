namespace SMGS.Application.DTOs.Commerce;

public class SePayOptions
{
    public string Bank { get; set; } = "TPBank";
    public string AccountNumber { get; set; } = "11140845389";
    public string AccountHolder { get; set; } = "NGUYEN DANG KHOA";
    public string WebhookSecret { get; set; } = "";
}

public record SePayPaymentResponse(Guid Id, Guid OrderId, string Status, decimal Amount, string Currency,
    string Bank, string AccountNumber, string AccountHolder, string TransferContent, string QrUrl);

public class SePayWebhookRequest
{
    public long Id { get; set; }
    public string? Gateway { get; set; }
    public string? AccountNumber { get; set; }
    public string? Code { get; set; }
    public string? Content { get; set; }
    public string? TransferType { get; set; }
    public decimal TransferAmount { get; set; }
}

public record SePayWebhookResponse(bool Success, bool Processed, string Message);
