using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Commerce;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class CommerceService(ApplicationDbContext db) : ICommerceService
{
    public Task EnsureMuseumManagement(int museumId, Guid staff, bool admin) => StaffAccess(museumId, staff, admin);
    private async Task StaffAccess(int museum, Guid user, bool admin)
    {
        if (!admin && !await db.StaffAssignments.AnyAsync(a => a.UserId == user && a.MuseumId == museum
            && a.GalleryId == null && a.ArtifactId == null))
            throw new CommerceException("Bạn không được phân công quản lý bảo tàng này.", 403);
        if (!await db.Museums.AnyAsync(m => m.Id == museum)) throw new CommerceException("Không tìm thấy bảo tàng.", 404);
    }

    private static int[] ResourceIds(string json) => JsonSerializer.Deserialize<int[]>(json)!;
    private static ProductResponse Map(CommerceProduct p) => new(p.Id, p.MuseumId, p.Name, p.Kind, p.Price,
        p.Currency, p.Seats, p.ValidityHours, p.ValidityStartsAt, ResourceIds(p.ResourceIdsJson), p.IsActive);
    private static OrderResponse Map(PurchaseOrder o) => new(o.Id, o.MuseumId, o.ProductName, o.ProductKind,
        o.Amount, o.Currency, o.Seats, o.ValidityHours, o.ValidityStartsAt, ResourceIds(o.ResourceIdsJson), o.BuyerName,
        JsonSerializer.Deserialize<Dictionary<string, string>>(o.BuyerDetailsJson)!, o.Status, o.CreatedAt, o.PaidAt, o.ExpiresAt);
    private async Task<PurchaseOrder> LockOrder(Guid id) => await db.PurchaseOrders
        .FromSqlInterpolated($"SELECT * FROM purchase_orders WHERE \"Id\" = {id} FOR UPDATE")
        .SingleOrDefaultAsync() ?? throw new CommerceException("Không tìm thấy đơn hàng.", 404);
    private static void Owner(PurchaseOrder order, Guid buyer)
    {
        if (order.BuyerId != buyer) throw new CommerceException("Không tìm thấy đơn hàng.", 404);
    }
    private static void Paid(PurchaseOrder order)
    {
        if (order.Status != "paid" || order.ExpiresAt == null || order.ExpiresAt <= DateTime.UtcNow)
            throw new CommerceException("Gói chưa thanh toán hoặc đã hết hạn.", 409);
    }

    public async Task<IReadOnlyList<CommerceResource>> Resources(int museumId) => await db.CommerceResources
        .AsNoTracking().Where(r => r.MuseumId == museumId).OrderBy(r => r.Id).ToListAsync();

    public async Task<CommerceResource> ConfigureResource(int museumId, int? id, ResourceRequest r, Guid staff, bool admin)
    {
        await StaffAccess(museumId, staff, admin);
        if (r.ArtifactId != null && (r.Kind != "digital" || !await db.Artifacts.AnyAsync(a => a.Id == r.ArtifactId && a.MuseumId == museumId)))
            throw new CommerceException("Hiện vật phải thuộc bảo tàng và là nội dung số.");
        if (r.ArtifactId != null && await db.CommerceResources.AnyAsync(x => x.ArtifactId == r.ArtifactId && x.Id != id))
            throw new CommerceException("Hiện vật đã có cấu hình quyền truy cập.", 409);
        var resource = id == null ? new CommerceResource { MuseumId = museumId } :
            await db.CommerceResources.SingleOrDefaultAsync(x => x.Id == id && x.MuseumId == museumId)
                ?? throw new CommerceException("Không tìm thấy dịch vụ.", 404);
        // Resource identity/scope is immutable: existing purchases retain the same entitlement.
        if (id != null && (resource.Kind != r.Kind || resource.ArtifactId != r.ArtifactId || resource.ScopeReference != r.ScopeReference))
            throw new CommerceException("Không đổi phạm vi dịch vụ đã tạo. Hãy tạo dịch vụ mới.", 409);
        resource.Name = r.Name.Trim(); resource.Kind = r.Kind; resource.AccessLevel = r.AccessLevel;
        resource.ArtifactId = r.ArtifactId; resource.ScopeReference = r.ScopeReference;
        if (id == null) db.CommerceResources.Add(resource);
        if (r.ArtifactId != null)
            (await db.Artifacts.SingleAsync(a => a.Id == r.ArtifactId)).AccessLevel = r.AccessLevel;
        await db.SaveChangesAsync(); return resource;
    }

    public async Task<IReadOnlyList<ProductResponse>> Products(int museumId, bool includeInactive, Guid? staff, bool admin)
    {
        if (includeInactive) await StaffAccess(museumId, staff ?? Guid.Empty, admin);
        return (await db.CommerceProducts.AsNoTracking().Where(p => p.MuseumId == museumId && (includeInactive || p.IsActive))
            .OrderBy(p => p.Id).ToListAsync()).Select(Map).ToList();
    }

    public async Task<ProductResponse> ConfigureProduct(int museumId, int? id, ProductRequest r, Guid staff, bool admin)
    {
        await StaffAccess(museumId, staff, admin);
        if (r.IsActive && (r.ValidityHours == null || r.ValidityStartsAt != "payment"))
            throw new CommerceException("Cần xác nhận thời hạn và mốc bắt đầu sử dụng trước khi mở bán. Hiện hỗ trợ mốc thanh toán.");
        var ids = r.ResourceIds.Distinct().Order().ToArray();
        var resources = await db.CommerceResources.Where(x => ids.Contains(x.Id) && x.MuseumId == museumId).ToListAsync();
        if (ids.Length == 0 || resources.Count != ids.Length) throw new CommerceException("Danh sách dịch vụ phải thuộc bảo tàng.");
        var admission = resources.Any(x => x.Kind == "admission");
        var digital = resources.Any(x => x.Kind == "digital");
        if (r.Kind == "admission" && (!admission || digital) || r.Kind == "digital" && (!digital || admission)
            || r.Kind == "combo" && (!admission || !digital)) throw new CommerceException("Phạm vi dịch vụ không khớp loại sản phẩm.");
        var p = id == null ? new CommerceProduct { MuseumId = museumId } :
            await db.CommerceProducts.SingleOrDefaultAsync(x => x.Id == id && x.MuseumId == museumId)
                ?? throw new CommerceException("Không tìm thấy sản phẩm.", 404);
        p.Name = r.Name.Trim(); p.Kind = r.Kind; p.Price = r.Price; p.Currency = r.Currency;
        p.Seats = r.Seats; p.ValidityHours = r.ValidityHours; p.ValidityStartsAt = r.ValidityStartsAt;
        p.IsActive = r.IsActive; p.ResourceIdsJson = JsonSerializer.Serialize(ids);
        if (id == null) db.CommerceProducts.Add(p);
        await db.SaveChangesAsync(); return Map(p);
    }

    public async Task<OrderResponse> CreateOrder(OrderRequest r, Guid buyer)
    {
        if (r.BuyerDetails == null || r.BuyerDetails.Count > 30 || r.BuyerDetails.Any(k => k.Key.Length > 100 || k.Value == null || k.Value.Length > 2000))
            throw new CommerceException("Thông tin người mua quá dài hoặc không hợp lệ.");
        var p = await db.CommerceProducts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == r.ProductId && p.IsActive)
            ?? throw new CommerceException("Sản phẩm không được mở bán.", 404);
        if (p.ValidityHours == null || p.ValidityStartsAt != "payment") throw new CommerceException("Chưa xác nhận thời hạn sử dụng.", 409);
        if (!await db.Museums.AnyAsync(m => m.Id == p.MuseumId && m.IsActive)) throw new CommerceException("Bảo tàng ngừng hoạt động.", 409);
        var o = new PurchaseOrder { BuyerId = buyer, MuseumId = p.MuseumId, ProductId = p.Id, ProductName = p.Name,
            ProductKind = p.Kind, Amount = p.Price, Currency = p.Currency, Seats = p.Seats, ValidityHours = p.ValidityHours.Value,
            ValidityStartsAt = p.ValidityStartsAt, ResourceIdsJson = p.ResourceIdsJson,
            BuyerName = r.BuyerName.Trim(), BuyerDetailsJson = JsonSerializer.Serialize(r.BuyerDetails) };
        db.PurchaseOrders.Add(o); await db.SaveChangesAsync(); return Map(o);
    }

    public async Task<IReadOnlyList<OrderResponse>> Orders(Guid buyer, int page, int pageSize) =>
        (await db.PurchaseOrders.AsNoTracking().Where(o => o.BuyerId == buyer).OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100)).Take(Math.Clamp(pageSize, 1, 100)).ToListAsync()).Select(Map).ToList();

    public async Task<PaymentAttempt> StartPayment(Guid orderId, Guid buyer)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var o = await LockOrder(orderId); Owner(o, buyer);
        if (o.Status != "pending") throw new CommerceException("Đơn hàng đã thanh toán.", 409);
        // Repeating a request returns the same pending attempt. Retry after failure creates a new record.
        var attempt = await db.PaymentAttempts.SingleOrDefaultAsync(a => a.OrderId == orderId && a.Status == "pending");
        if (attempt == null) { attempt = new PaymentAttempt { OrderId = orderId }; db.PaymentAttempts.Add(attempt); }
        await db.SaveChangesAsync(); await tx.CommitAsync(); return attempt;
    }

    public async Task<IReadOnlyList<PaymentAttempt>> Transactions(Guid orderId, Guid buyer)
    {
        var o = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId)
            ?? throw new CommerceException("Không tìm thấy đơn hàng.", 404);
        Owner(o, buyer);
        return await db.PaymentAttempts.AsNoTracking().Where(a => a.OrderId == orderId).OrderBy(a => a.CreatedAt).ToListAsync();
    }

    public async Task<PaymentAttempt> ConfirmPayment(Guid attemptId, ConfirmPaymentRequest r, Guid staff, bool admin)
    {
        var found = await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == attemptId)
            ?? throw new CommerceException("Không tìm thấy giao dịch.", 404);
        await using var tx = await db.Database.BeginTransactionAsync();
        var o = await LockOrder(found.OrderId); await StaffAccess(o.MuseumId, staff, admin);
        var a = await db.PaymentAttempts.SingleAsync(a => a.Id == attemptId);
        var status = r.Succeeded ? "succeeded" : "failed";
        var reference = string.IsNullOrWhiteSpace(r.Reference) ? null : r.Reference.Trim();
        if (a.Status != "pending")
        {
            if (a.Status != status || a.Reference != reference) throw new CommerceException("Giao dịch đã được xác nhận với kết quả khác.", 409);
            return a;
        }
        if (o.Status != "pending") throw new CommerceException("Đơn hàng đã thanh toán.", 409);
        if (r.Succeeded && reference == null) throw new CommerceException("Cần mã tham chiếu xác nhận thanh toán.");
        if (!r.Succeeded && string.IsNullOrWhiteSpace(r.FailureReason)) throw new CommerceException("Cần lý do giao dịch thất bại.");
        if (reference != null && await db.PaymentAttempts.AnyAsync(x => x.Id != a.Id && x.Reference == reference))
            throw new CommerceException("Mã tham chiếu đã được sử dụng.", 409);
        a.Status = status; a.Reference = reference; a.FailureReason = r.Succeeded ? null : r.FailureReason?.Trim();
        a.ConfirmedBy = staff; a.CompletedAt = DateTime.UtcNow;
        if (r.Succeeded)
        {
            o.Status = "paid"; o.PaidAt = a.CompletedAt; o.ExpiresAt = a.CompletedAt.Value.AddHours(o.ValidityHours);
            if (o.Seats == 1) db.MuseumPasses.Add(new MuseumPass { OrderId = o.Id, UserId = o.BuyerId });
            db.PurchaseInvoices.Add(new PurchaseInvoice { OrderId = o.Id, Number = $"SMGS-{o.Id:N}", BuyerName = o.BuyerName,
                BuyerDetailsJson = o.BuyerDetailsJson, ProductName = o.ProductName, ProductKind = o.ProductKind, Seats = o.Seats,
                Amount = o.Amount, Currency = o.Currency });
        }
        await db.SaveChangesAsync(); await tx.CommitAsync(); return a;
    }

    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    public async Task<GroupKeyResponse> IssueGroupKey(Guid orderId, Guid buyer)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var o = await LockOrder(orderId); Owner(o, buyer); Paid(o);
        if (o.Seats <= 1) throw new CommerceException("Chỉ gói nhiều người có key.");
        var remaining = o.Seats - await db.MuseumPasses.CountAsync(p => p.OrderId == orderId);
        if (remaining <= 0) throw new CommerceException("Gói đã đủ người nhận.", 409);
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); o.GroupKeyHash = Hash(key);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return new(key, remaining);
    }

    public async Task<MuseumPass> Redeem(string key, Guid user)
    {
        var hash = Hash(key);
        var id = await db.PurchaseOrders.Where(o => o.GroupKeyHash == hash).Select(o => (Guid?)o.Id).SingleOrDefaultAsync()
            ?? throw new CommerceException("Key không hợp lệ.", 404);
        await using var tx = await db.Database.BeginTransactionAsync();
        var o = await LockOrder(id);
        if (o.GroupKeyHash != hash) throw new CommerceException("Key đã được thay đổi.", 409);
        Paid(o);
        var existing = await db.MuseumPasses.SingleOrDefaultAsync(p => p.OrderId == id && p.UserId == user);
        if (existing != null)
        {
            if (existing.RevokedAt != null) throw new CommerceException("Pass đã bị thu hồi.", 403);
            return existing;
        }
        if (await db.MuseumPasses.CountAsync(p => p.OrderId == id) >= o.Seats) throw new CommerceException("Key đã hết lượt nhận.", 409);
        var pass = new MuseumPass { OrderId = id, UserId = user }; db.MuseumPasses.Add(pass);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return pass;
    }

    public async Task<IReadOnlyList<PassResponse>> Passes(Guid user, int page, int pageSize)
    {
        var items = await (from p in db.MuseumPasses.AsNoTracking() join o in db.PurchaseOrders on p.OrderId equals o.Id
            where p.UserId == user orderby p.ClaimedAt descending, p.Id select new { Pass = p, Order = o })
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100)).Take(Math.Clamp(pageSize, 1, 100)).ToListAsync();
        return items.Select(x => new PassResponse(x.Pass.Id, x.Order.Id, x.Order.MuseumId, x.Order.ProductName,
            ResourceIds(x.Order.ResourceIdsJson), x.Pass.ClaimedAt, x.Order.ExpiresAt, x.Pass.RevokedAt,
            x.Pass.RevokedAt == null && x.Order.Status == "paid" && x.Order.ExpiresAt > DateTime.UtcNow)).ToList();
    }

    public async Task<IReadOnlyList<OrderResponse>> MuseumOrders(int museumId, Guid staff, bool admin, int page, int pageSize)
    {
        await StaffAccess(museumId, staff, admin);
        return (await db.PurchaseOrders.AsNoTracking().Where(o => o.MuseumId == museumId).OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100)).Take(Math.Clamp(pageSize, 1, 100)).ToListAsync()).Select(Map).ToList();
    }

    public async Task<ManagedOrderResponse> ManageOrder(Guid orderId, Guid staff, bool admin)
    {
        var o = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId)
            ?? throw new CommerceException("Không tìm thấy đơn hàng.", 404);
        await StaffAccess(o.MuseumId, staff, admin);
        var attempts = await db.PaymentAttempts.AsNoTracking().Where(a => a.OrderId == orderId).OrderBy(a => a.CreatedAt)
            .Select(a => new PaymentAttemptResponse(a.Id, a.Status, a.Reference, a.FailureReason, a.CreatedAt, a.CompletedAt)).ToListAsync();
        var invoice = await db.PurchaseInvoices.AsNoTracking().SingleOrDefaultAsync(i => i.OrderId == orderId);
        return new(Map(o), attempts, invoice);
    }

    public async Task RevokePass(Guid passId, Guid staff, bool admin)
    {
        var pass = await db.MuseumPasses.SingleOrDefaultAsync(p => p.Id == passId) ?? throw new CommerceException("Không tìm thấy pass.", 404);
        var museum = await db.PurchaseOrders.Where(o => o.Id == pass.OrderId).Select(o => o.MuseumId).SingleAsync();
        await StaffAccess(museum, staff, admin);
        pass.RevokedAt ??= DateTime.UtcNow; await db.SaveChangesAsync();
    }

    public async Task<PurchaseInvoice> Invoice(Guid orderId, Guid buyer)
    {
        var o = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId)
            ?? throw new CommerceException("Không tìm thấy đơn hàng.", 404);
        Owner(o, buyer);
        return await db.PurchaseInvoices.AsNoTracking().SingleOrDefaultAsync(i => i.OrderId == orderId)
            ?? throw new CommerceException("Chưa có hóa đơn.", 404);
    }

    public async Task<IReadOnlyList<RevenueResponse>> Revenue(int museumId, DateTime fromUtc, DateTime toUtc, Guid staff, bool admin)
    {
        await StaffAccess(museumId, staff, admin);
        if (fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc || toUtc <= fromUtc || toUtc - fromUtc > TimeSpan.FromDays(366))
            throw new CommerceException("Chọn khoảng thời gian UTC hợp lệ, tối đa 366 ngày (từ bao gồm, đến không bao gồm).");
        var totals = await db.PurchaseOrders.Where(o => o.MuseumId == museumId && o.Status == "paid" && o.PaidAt >= fromUtc && o.PaidAt < toUtc)
            .GroupBy(o => o.Currency).Select(g => new { Currency = g.Key, Count = g.Count(), Total = g.Sum(o => o.Amount) }).ToListAsync();
        var failures = await (from a in db.PaymentAttempts join o in db.PurchaseOrders on a.OrderId equals o.Id
            where o.MuseumId == museumId && a.Status == "failed" && a.CompletedAt >= fromUtc && a.CompletedAt < toUtc
            group a by o.Currency into g select new { Currency = g.Key, Count = g.Count() }).ToListAsync();
        return totals.Select(x => x.Currency).Union(failures.Select(x => x.Currency)).Order()
            .Select(c => new RevenueResponse(c, totals.FirstOrDefault(x => x.Currency == c)?.Count ?? 0,
                totals.FirstOrDefault(x => x.Currency == c)?.Total ?? 0, failures.FirstOrDefault(x => x.Currency == c)?.Count ?? 0)).ToList();
    }

    public async Task<AccessResponse> Access(int resourceId, Guid? user)
    {
        var r = await db.CommerceResources.AsNoTracking().SingleOrDefaultAsync(r => r.Id == resourceId)
            ?? throw new CommerceException("Không tìm thấy dịch vụ.", 404);
        if (r.AccessLevel == "free") return new(true, "free");
        var scopes = await (from p in db.MuseumPasses join o in db.PurchaseOrders on p.OrderId equals o.Id
            where p.UserId == user && p.RevokedAt == null && o.MuseumId == r.MuseumId && o.Status == "paid" && o.ExpiresAt > DateTime.UtcNow
            select o.ResourceIdsJson).ToListAsync();
        return new(scopes.Any(s => ResourceIds(s).Contains(resourceId)), "premium");
    }

    public async Task<bool> CanReadArtifact(int artifactId, Guid? user)
    {
        var a = await db.Artifacts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == artifactId && a.IsActive);
        if (a == null) return false;
        var r = await db.CommerceResources.AsNoTracking().SingleOrDefaultAsync(r => r.ArtifactId == artifactId);
        if (r == null) return a.AccessLevel == "free";
        return (await Access(r.Id, user)).Allowed;
    }
}

