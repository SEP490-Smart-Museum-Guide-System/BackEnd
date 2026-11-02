using SMGS.Application.DTOs.Commerce;
using SMGS.Domain.Entities;

namespace SMGS.Application.Interfaces;

public interface ICommerceService
{
    Task EnsureMuseumManagement(int museumId, Guid staff, bool admin);
    Task<IReadOnlyList<CommerceResource>> Resources(int museumId);
    Task<CommerceResource> ConfigureResource(int museumId, int? id, ResourceRequest request, Guid staff, bool admin);
    Task<IReadOnlyList<ProductResponse>> Products(int museumId, bool includeInactive, Guid? staff, bool admin);
    Task<ProductResponse> ConfigureProduct(int museumId, int? id, ProductRequest request, Guid staff, bool admin);
    Task<OrderResponse> CreateOrder(OrderRequest request, Guid buyer);
    Task<IReadOnlyList<OrderResponse>> Orders(Guid buyer, int page, int pageSize);
    Task<PaymentAttempt> StartPayment(Guid orderId, Guid buyer);
    Task<IReadOnlyList<PaymentAttempt>> Transactions(Guid orderId, Guid buyer);
    Task<PaymentAttempt> ConfirmPayment(Guid attemptId, ConfirmPaymentRequest request, Guid staff, bool admin);
    Task<GroupKeyResponse> IssueGroupKey(Guid orderId, Guid buyer);
    Task<MuseumPass> Redeem(string key, Guid user);
    Task<IReadOnlyList<PassResponse>> Passes(Guid user, int page, int pageSize);
    Task<IReadOnlyList<OrderResponse>> MuseumOrders(int museumId, Guid staff, bool admin, int page, int pageSize);
    Task<ManagedOrderResponse> ManageOrder(Guid orderId, Guid staff, bool admin);
    Task RevokePass(Guid passId, Guid staff, bool admin);
    Task<PurchaseInvoice> Invoice(Guid orderId, Guid buyer);
    Task<IReadOnlyList<RevenueResponse>> Revenue(int museumId, DateTime from, DateTime to, Guid staff, bool admin);
    Task<AccessResponse> Access(int resourceId, Guid? user);
    Task<bool> CanReadArtifact(int artifactId, Guid? user);
}
