using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Commerce;

namespace SMGS.Infrastructure.Services;

public partial class CommerceService
{
    private SePayOptions SePay => sepayOptions?.Value ?? new SePayOptions();

    public async Task<SePayPaymentResponse> StartSePayPayment(Guid orderId, Guid buyer)
    {
        if (string.IsNullOrWhiteSpace(SePay.WebhookSecret) || string.IsNullOrWhiteSpace(SePay.Bank)
            || string.IsNullOrWhiteSpace(SePay.AccountNumber) || string.IsNullOrWhiteSpace(SePay.AccountHolder))
            throw new CommerceException("Chưa cấu hình thanh toán SePay.", 503);
        var order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId)
            ?? throw new CommerceException("Không tìm thấy đơn hàng.", 404);
        Owner(order, buyer);
        if (order.Currency != "VND" || order.Amount <= 0 || decimal.Truncate(order.Amount) != order.Amount)
            throw new CommerceException("SePay chỉ hỗ trợ số tiền VND nguyên dương.", 409);
        var attempt = await StartPayment(orderId, buyer);
        var content = $"SMGS{attempt.Id:N}".ToUpperInvariant();
        string E(string value) => Uri.EscapeDataString(value);
        var url = $"https://vietqr.app/img?bank={E(SePay.Bank)}&acc={E(SePay.AccountNumber)}&template=compact"
            + $"&amount={order.Amount.ToString("0", CultureInfo.InvariantCulture)}&des={E(content)}&showinfo=true&holder={E(SePay.AccountHolder)}";
        return new(attempt.Id, orderId, attempt.Status, order.Amount, order.Currency,
            SePay.Bank, SePay.AccountNumber, SePay.AccountHolder, content, url);
    }

    public static bool VerifySePaySignature(byte[] rawBody, string signature, string timestamp, string secret, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secret) || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds < now.ToUnixTimeSeconds() - 300 || seconds > now.ToUnixTimeSeconds() + 300
            || !signature.StartsWith("sha256=", StringComparison.Ordinal) || signature.Length != 71) return false;
        byte[] received;
        try { received = Convert.FromHexString(signature[7..]); }
        catch (FormatException) { return false; }
        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var signed = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(signed, 0); rawBody.CopyTo(signed, prefix.Length);
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    public async Task<SePayWebhookResponse> SePayWebhook(byte[] rawBody, string signature, string timestamp)
    {
        if (string.IsNullOrWhiteSpace(SePay.WebhookSecret))
            throw new CommerceException("Chưa cấu hình webhook SePay.", 503);
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        if (!VerifySePaySignature(rawBody, signature, timestamp, SePay.WebhookSecret, now))
            throw new CommerceException("Chữ ký SePay không hợp lệ hoặc đã hết hạn.", 401);
        SePayWebhookRequest payload;
        try { payload = JsonSerializer.Deserialize<SePayWebhookRequest>(rawBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new JsonException(); }
        catch (JsonException) { throw new CommerceException("Dữ liệu webhook không hợp lệ."); }
        if (payload.Id <= 0 || payload.TransferAmount <= 0 || string.IsNullOrWhiteSpace(payload.AccountNumber)
            || string.IsNullOrWhiteSpace(payload.Gateway) || payload.TransferType is not ("in" or "out"))
            throw new CommerceException("Thiếu thông tin giao dịch SePay hợp lệ.");
        if (payload.TransferType != "in" || payload.AccountNumber != SePay.AccountNumber
            || !string.Equals(payload.Gateway, SePay.Bank, StringComparison.OrdinalIgnoreCase))
            return new(true, false, "Giao dịch không thuộc tài khoản nhận tiền.");

        // Match one complete code, never an arbitrary substring or ambiguous set of codes.
        var matches = Regex.Matches((payload.Code ?? "") + " " + (payload.Content ?? ""),
            @"(?<![A-Z0-9])SMGS([A-F0-9]{32})(?![A-Z0-9])", RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));
        var ids = matches.Select(m => Guid.ParseExact(m.Groups[1].Value, "N")).Distinct().ToArray();
        if (ids.Length != 1) return new(true, false, "Không xác định được mã thanh toán.");
        var found = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == ids[0]);
        if (found == null) return new(true, false, "Không tìm thấy giao dịch thanh toán.");
        var reference = "SEPAY:" + payload.Id.ToString(CultureInfo.InvariantCulture);
        await using var tx = await db.Database.BeginTransactionAsync();
        var order = await LockOrder(found.OrderId);
        var attempt = await db.PaymentAttempts.SingleAsync(a => a.Id == found.Id);
        if (order.Currency != "VND" || order.Amount != payload.TransferAmount)
            return new(true, false, "Số tiền không khớp đơn hàng; cần đối soát.");
        if (attempt.Status == "succeeded" && attempt.Reference == reference)
            return new(true, true, "Giao dịch đã được xử lý.");
        if (attempt.Status != "pending" || order.Status != "pending")
            return new(true, false, "Đơn hàng hoặc giao dịch đã đóng; cần đối soát.");
        if (await db.PaymentAttempts.AnyAsync(a => a.Reference == reference))
            throw new CommerceException("Giao dịch SePay đã được sử dụng.", 409);
        attempt.Status = "succeeded"; attempt.Reference = reference; attempt.CompletedAt = now.UtcDateTime;
        CompleteOrder(order, now.UtcDateTime);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return new(true, true, "Thanh toán thành công.");
    }
}
