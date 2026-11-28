using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Commerce;
using SMGS.Domain.Entities;

namespace SMGS.Infrastructure.Services;

public partial class CommerceService
{
    private const string AdmissionKind = "admission";
    private const string CombinedKind = "admission_and_digital";

    public async Task<IReadOnlyList<AdmissionOffer>> AdmissionOffers(int museumId, Guid? staff, bool admin)
    {
        if (staff.HasValue) await StaffAccess(museumId, staff.Value, admin);
        return await db.CommerceProducts.AsNoTracking()
            .Where(p => p.MuseumId == museumId && p.Kind == AdmissionKind && (staff.HasValue || p.IsActive))
            .OrderBy(p => p.Id)
            .Select(p => new AdmissionOffer(p.Id, p.MuseumId, p.Name, p.Price, p.Currency, p.IsActive))
            .ToListAsync();
    }

    public async Task<AdmissionOffer> SaveAdmissionOffer(int museumId, int? id, AdmissionOfferRequest request, Guid staff, bool admin)
    {
        await StaffAccess(museumId, staff, admin);
        var product = id.HasValue
            ? await db.CommerceProducts.SingleOrDefaultAsync(p => p.Id == id && p.MuseumId == museumId && p.Kind == AdmissionKind)
                ?? throw new CommerceException("Không tìm thấy cấu hình vé.", 404)
            : new CommerceProduct { MuseumId = museumId, Kind = AdmissionKind, ValidityHours = 24 };
        product.Name = request.Name.Trim();
        product.Price = request.Price;
        product.IsActive = request.IsActive;
        if (!id.HasValue) db.CommerceProducts.Add(product);
        await db.SaveChangesAsync();
        return new(product.Id, museumId, product.Name, product.Price, product.Currency, product.IsActive);
    }

    public Task<OrderResponse> CreateAdmissionOrder(int museumId, AdmissionOrderRequest request, Guid buyer)
        => CreateOrderCore(request, buyer, museumId, request.Quantity, request.DigitalProductId);

    private async Task<AdmissionTicket> TicketForOrder(Guid orderId) =>
        await db.Set<AdmissionTicket>().SingleOrDefaultAsync(t => t.OrderId == orderId)
        ?? throw new CommerceException("Không tìm thấy vé.", 404);

    private async Task<int> UsedSeats(Guid orderId) => await db.Set<AdmissionCheckIn>()
        .Where(c => c.OrderId == orderId).SumAsync(c => c.Quantity);

    private static AdmissionTicketResponse TicketResponse(PurchaseOrder order, AdmissionTicket ticket, int used)
    {
        var remaining = ticket.Quantity - used;
        return new(order.Id, order.MuseumId, order.VisitDate, ticket.Code, ticket.Quantity, used, remaining,
            order.Status == "paid" && ticket.CancelledAt == null && order.VisitDate == LocalToday() && remaining > 0);
    }

    public async Task<AdmissionTicketResponse> AdmissionTicketForBuyer(Guid orderId, Guid buyer)
    {
        var order = await BuyerOrder(orderId, buyer);
        if (order.PaidAt == null) throw new CommerceException("Vé chưa thanh toán.", 409);
        return TicketResponse(order, await TicketForOrder(orderId), await UsedSeats(orderId));
    }

    private async Task<PurchaseOrder> BuyerOrder(Guid orderId, Guid buyer) =>
        await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId && o.BuyerId == buyer)
        ?? throw new CommerceException("Không tìm thấy đơn hàng.", 404);

    public async Task<CheckoutDetails> AdmissionDetails(Guid orderId, Guid buyer)
    {
        var order = await BuyerOrder(orderId, buyer);
        var ticket = await TicketForOrder(orderId);
        return new(orderId, ticket.Amount, order.Amount - ticket.Amount, ticket.CancelledAt ?? order.CancelledAt,
            order.ProductKind == CombinedKind ? order.DigitalCancelledAt ?? order.CancelledAt : null,
            order.ProductKind == AdmissionKind && order.PaidAt.HasValue ? order.RefundedAmount : ticket.RefundedAmount,
            order.ProductKind == CombinedKind && order.DigitalCancelledAt.HasValue
                ? order.RefundedAmount - ticket.RefundedAmount : 0m);
    }

    public async Task<AdmissionTicketResponse> InspectAdmission(int museumId, string code, Guid staff, bool admin)
    {
        await StaffAccess(museumId, staff, admin);
        var ticket = await FindAdmissionByCode(code);
        var order = await db.PurchaseOrders.AsNoTracking().SingleAsync(o => o.Id == ticket.OrderId);
        CheckAdmissionMuseum(order, museumId);
        if (order.PaidAt == null) throw new CommerceException("Vé chưa thanh toán.", 409);
        return TicketResponse(order, ticket, await UsedSeats(order.Id));
    }

    private async Task<AdmissionTicket> FindAdmissionByCode(string code) =>
        await db.Set<AdmissionTicket>().AsNoTracking().SingleOrDefaultAsync(t => t.Code == code)
        ?? throw new CommerceException("Không tìm thấy mã vé.", 404);

    private static void CheckAdmissionMuseum(PurchaseOrder order, int museumId)
    {
        if (order.MuseumId != museumId) throw new CommerceException("Vé thuộc bảo tàng khác.", 409);
    }

    public async Task<AdmissionCheckInResponse> CheckInAdmission(int museumId, AdmissionCheckInRequest request, Guid staff, bool admin)
    {
        if (request.OperationId == Guid.Empty || request.Quantity < 1)
            throw new CommerceException("Cần mã thao tác và số người hợp lệ.");
        await StaffAccess(museumId, staff, admin);
        var found = await FindAdmissionByCode(request.Code);
        await using var tx = await db.Database.BeginTransactionAsync();
        var order = await LockOrder(found.OrderId);
        CheckAdmissionMuseum(order, museumId);
        var ticket = await TicketForOrder(order.Id);
        var entry = await db.Set<AdmissionCheckIn>().FindAsync(order.Id, request.OperationId);
        var used = await UsedSeats(order.Id);
        if (entry != null)
        {
            if (entry.Quantity != request.Quantity || entry.StaffId != staff)
                throw new CommerceException("Mã thao tác đã dùng cho lần xác nhận khác.", 409);
        }
        else
        {
            var current = TicketResponse(order, ticket, used);
            if (!current.CanCheckIn || request.Quantity > current.Remaining)
                throw new CommerceException("Vé sai ngày, đã hủy, chưa thanh toán hoặc không đủ lượt.", 409);
            entry = new AdmissionCheckIn { OrderId = order.Id, OperationId = request.OperationId,
                StaffId = staff, Quantity = request.Quantity, CheckedInAt = DateTime.UtcNow };
            db.Add(entry);
            await db.SaveChangesAsync();
            used += entry.Quantity;
        }
        await tx.CommitAsync();
        return new(entry.OperationId, entry.Quantity, entry.CheckedInAt, TicketResponse(order, ticket, used));
    }

    public async Task<CancellationResponse> CancelAdmissionItem(Guid orderId, string item, Guid buyer)
    {
        if (item is not ("ticket" or "digital")) throw new CommerceException("Món cần hủy không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({buyer.ToString()}, 0))");
        var order = await LockOrder(orderId);
        Owner(order, buyer);
        if (order.ProductKind != CombinedKind || order.Status != "paid")
            throw new CommerceException("Chỉ hủy từng món trong đơn mua chung đã thanh toán.", 409);
        var ticket = await TicketForOrder(orderId);
        var cancelTicket = item == "ticket";
        if (cancelTicket ? ticket.CancelledAt.HasValue : order.DigitalCancelledAt.HasValue)
            throw new CommerceException("Món này đã được hủy.", 409);
        if (cancelTicket && await UsedSeats(orderId) > 0)
            throw new CommerceException("Vé đã check-in và không thể hủy.", 409);
        var daysBefore = order.VisitDate.DayNumber - LocalToday().DayNumber;
        var rate = daysBefore >= 3 ? 1m : daysBefore == 2 ? 0.5m : 0m;
        var refund = decimal.Round((cancelTicket ? ticket.Amount : order.Amount - ticket.Amount) * rate, 2, MidpointRounding.AwayFromZero);
        var now = DateTime.UtcNow;
        if (cancelTicket)
        {
            ticket.CancelledAt = now;
            ticket.RefundedAmount = refund;
        }
        else order.DigitalCancelledAt = now;
        if (ticket.CancelledAt.HasValue && order.DigitalCancelledAt.HasValue)
        {
            order.Status = "cancelled";
            order.CancelledAt = now;
        }
        order.RefundedAmount += refund;
        if (refund > 0) db.WalletTransactions.Add(new WalletTransaction { UserId = buyer, OrderId = orderId,
            Amount = refund, Currency = order.Currency, Type = "refund", Description = $"Hoàn tiền món {item} của đơn {orderId:N}" });
        await db.SaveChangesAsync();
        var balance = await db.WalletTransactions.Where(t => t.UserId == buyer && t.Currency == order.Currency)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        await tx.CommitAsync();
        return new(orderId, order.Status, refund, balance, "Đã hủy món đã chọn.");
    }
}
