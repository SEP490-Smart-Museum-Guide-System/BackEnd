using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Commerce;
using SMGS.Domain.Entities;

namespace SMGS.Infrastructure.Services;

public partial class CommerceService
{
    private SePayOptions SePay => sepayOptions?.Value ?? new SePayOptions();

    public async Task<SePayPaymentResponse> StartSePayPayment(Guid orderId, Guid buyer)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({buyer.ToString()}, 0))");
        var order = await LockOrder(orderId); Owner(order, buyer);
        if (order.Status == "paid")
        {
            var paidAttempt = await db.PaymentAttempts.SingleAsync(a => a.OrderId == order.Id && a.Status == "succeeded");
            await tx.CommitAsync();
            return new(paidAttempt.Id, order.Id, paidAttempt.Status, 0, order.Currency, "", "", "", "", "",
                paidAttempt.Reference?.StartsWith("WALLET:", StringComparison.Ordinal) == true);
        }
        if (order.Status != "pending") throw new CommerceException("Đơn hàng đã đóng hoặc đã hủy.", 409);
        if (order.VisitDate < LocalToday()) throw new CommerceException("Ngày tham quan đã qua; không thể thanh toán đơn này.", 409);
        EnsureBookingUsable(order.VisitDate, order.ValidityHours);

        var alreadyPaidByWallet = -(await db.WalletTransactions.Where(t => t.OrderId == order.Id && t.Type == "payment")
            .SumAsync(t => (decimal?)t.Amount) ?? 0m);
        if (alreadyPaidByWallet < 0 || alreadyPaidByWallet > order.Amount)
            throw new CommerceException("Số tiền đã trừ từ ví không hợp lệ; cần đối soát.", 409);
        var walletBalance = await db.WalletTransactions.Where(t => t.UserId == buyer && t.Currency == order.Currency)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var walletContribution = Math.Min(order.Amount - alreadyPaidByWallet, Math.Max(0, walletBalance));
        if (walletContribution > 0)
        {
            db.WalletTransactions.Add(new WalletTransaction
            {
                UserId = buyer, OrderId = order.Id, Amount = -walletContribution, Currency = order.Currency,
                Type = "payment", Description = $"Thanh toán một phần đơn {order.Id:N} bằng ví"
            });
            alreadyPaidByWallet += walletContribution;
        }

        var amountDue = order.Amount - alreadyPaidByWallet;
        if (amountDue == 0)
        {
            var attempt = await db.PaymentAttempts.SingleOrDefaultAsync(a => a.OrderId == order.Id && a.Status == "pending");
            var now = DateTime.UtcNow;
            if (attempt == null)
            {
                attempt = new PaymentAttempt { OrderId = order.Id };
                db.PaymentAttempts.Add(attempt);
            }
            attempt.Status = "succeeded"; attempt.Reference = $"WALLET:{Guid.NewGuid():N}"; attempt.CompletedAt = now;
            CompleteOrder(order, now);
            await db.SaveChangesAsync(); await tx.CommitAsync();
            await NotifyOrderPaidAsync(order);
            return new(attempt.Id, order.Id, attempt.Status, 0, order.Currency, "", "", "", "", "", true);
        }

        if (string.IsNullOrWhiteSpace(SePay.WebhookSecret) || string.IsNullOrWhiteSpace(SePay.Bank)
            || string.IsNullOrWhiteSpace(SePay.AccountNumber) || string.IsNullOrWhiteSpace(SePay.AccountHolder))
            throw new CommerceException("Chưa cấu hình thanh toán SePay.", 503);
        if (order.Currency != "VND" || amountDue <= 0 || decimal.Truncate(amountDue) != amountDue)
            throw new CommerceException("SePay chỉ hỗ trợ số tiền VND nguyên dương.", 409);
        var attemptPending = await db.PaymentAttempts.SingleOrDefaultAsync(a => a.OrderId == orderId && a.Status == "pending");
        if (attemptPending == null)
        {
            attemptPending = new PaymentAttempt { OrderId = order.Id };
            db.PaymentAttempts.Add(attemptPending);
        }
        await db.SaveChangesAsync(); await tx.CommitAsync();
        var attemptId = attemptPending.Id;
        var content = $"SMGS{attemptId:N}".ToUpperInvariant();
        string E(string value) => Uri.EscapeDataString(value);
        var url = $"https://vietqr.app/img?bank={E(SePay.Bank)}&acc={E(SePay.AccountNumber)}&template=compact"
            + $"&amount={amountDue.ToString("0", CultureInfo.InvariantCulture)}&des={E(content)}&showinfo=true&holder={E(SePay.AccountHolder)}";
        return new(attemptId, orderId, "pending", amountDue, order.Currency,
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
        if (order.VisitDate < DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now.UtcDateTime,
                TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh"))))
            return new(true, false, "Ngày tham quan đã qua; khoản tiền cần được đối soát.");
        var walletPaid = -(await db.WalletTransactions.Where(t => t.OrderId == order.Id && t.Type == "payment")
            .SumAsync(t => (decimal?)t.Amount) ?? 0m);
        if (order.Currency != "VND" || order.Amount - walletPaid != payload.TransferAmount)
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
        await NotifyOrderPaidAsync(order);
        return new(true, true, "Thanh toán thành công.");
    }
}
