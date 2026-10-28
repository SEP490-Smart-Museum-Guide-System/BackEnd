using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SMGS.Application.DTOs.Commerce;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

// Shared by the content routes so access failures use the project's response envelope.
public class CommerceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is CommerceException e)
        {
            context.Result = new ObjectResult(ResponseDto<object>.Fail(e.Message, e.StatusCode)) { StatusCode = e.StatusCode };
            context.ExceptionHandled = true;
        }
        else if (context.Exception is Microsoft.EntityFrameworkCore.DbUpdateException db &&
                 db.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            context.Result = new ConflictObjectResult(ResponseDto<object>.Fail("Dữ liệu trùng hoặc vừa được xử lý. Vui lòng tải lại.", 409));
            context.ExceptionHandled = true;
        }
    }
}

[ApiController]
[Route("api/commerce")]
[Authorize]
[TypeFilter(typeof(CommerceExceptionFilter))]
public class CommerceController(ICommerceService service) : ControllerBase
{
    private const string Managers = RoleNames.Admin + "," + RoleNames.MuseumStaff;
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new CommerceException("Vui lòng đăng nhập.", 401);
    private bool Admin => User.IsInRole(RoleNames.Admin);
    private IActionResult Result<T>(T data, int status = 200) => StatusCode(status, ResponseDto<T>.Success(data, statusCode: status));

    [AllowAnonymous, HttpGet("museums/{museumId:int}/resources")]
    public async Task<IActionResult> Resources(int museumId) => Result(await service.Resources(museumId));

    [Authorize(Roles = Managers), HttpPost("museums/{museumId:int}/resources")]
    public async Task<IActionResult> CreateResource(int museumId, ResourceRequest request) =>
        Result(await service.ConfigureResource(museumId, null, request, UserId, Admin), 201);

    [Authorize(Roles = Managers), HttpPut("museums/{museumId:int}/resources/{id:int}")]
    public async Task<IActionResult> UpdateResource(int museumId, int id, ResourceRequest request) =>
        Result(await service.ConfigureResource(museumId, id, request, UserId, Admin));

    [AllowAnonymous, HttpGet("museums/{museumId:int}/products")]
    public async Task<IActionResult> Products(int museumId) => Result(await service.Products(museumId, false, null, false));

    [Authorize(Roles = Managers), HttpGet("museums/{museumId:int}/products/manage")]
    public async Task<IActionResult> ManageProducts(int museumId) => Result(await service.Products(museumId, true, UserId, Admin));

    [Authorize(Roles = Managers), HttpPost("museums/{museumId:int}/products")]
    public async Task<IActionResult> CreateProduct(int museumId, ProductRequest request) =>
        Result(await service.ConfigureProduct(museumId, null, request, UserId, Admin), 201);

    [Authorize(Roles = Managers), HttpPut("museums/{museumId:int}/products/{id:int}")]
    public async Task<IActionResult> UpdateProduct(int museumId, int id, ProductRequest request) =>
        Result(await service.ConfigureProduct(museumId, id, request, UserId, Admin));

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(OrderRequest request) => Result(await service.CreateOrder(request, UserId), 201);

    [HttpGet("orders/me")]
    public async Task<IActionResult> Orders(int page = 1, int pageSize = 20) => Result(await service.Orders(UserId, page, pageSize));

    [Authorize(Roles = Managers), HttpGet("museums/{museumId:int}/orders")]
    public async Task<IActionResult> MuseumOrders(int museumId, int page = 1, int pageSize = 20) =>
        Result(await service.MuseumOrders(museumId, UserId, Admin, page, pageSize));

    [Authorize(Roles = Managers), HttpGet("orders/{orderId:guid}/manage")]
    public async Task<IActionResult> ManageOrder(Guid orderId) => Result(await service.ManageOrder(orderId, UserId, Admin));

    [HttpPost("orders/{orderId:guid}/payments")]
    public async Task<IActionResult> StartPayment(Guid orderId) => Result(await service.StartPayment(orderId, UserId));

    [HttpGet("orders/{orderId:guid}/transactions")]
    public async Task<IActionResult> Transactions(Guid orderId) => Result(await service.Transactions(orderId, UserId));

    [Authorize(Roles = Managers), HttpPost("payments/{attemptId:guid}/confirm")]
    public async Task<IActionResult> ConfirmPayment(Guid attemptId, ConfirmPaymentRequest request) =>
        Result(await service.ConfirmPayment(attemptId, request, UserId, Admin));

    [HttpPost("orders/{orderId:guid}/group-key")]
    public async Task<IActionResult> IssueGroupKey(Guid orderId) => Result(await service.IssueGroupKey(orderId, UserId));

    [HttpPost("passes/redeem")]
    public async Task<IActionResult> Redeem(RedeemKeyRequest request) => Result(await service.Redeem(request.Key, UserId));

    [HttpGet("passes/me")]
    public async Task<IActionResult> Passes(int page = 1, int pageSize = 20) => Result(await service.Passes(UserId, page, pageSize));

    [Authorize(Roles = Managers), HttpPost("passes/{passId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid passId)
    {
        await service.RevokePass(passId, UserId, Admin); return Result(new { Revoked = true });
    }

    [HttpGet("orders/{orderId:guid}/invoice")]
    public async Task<IActionResult> Invoice(Guid orderId) => Result(await service.Invoice(orderId, UserId));

    [Authorize(Roles = Managers), HttpGet("museums/{museumId:int}/revenue")]
    public async Task<IActionResult> Revenue(int museumId, DateTime from, DateTime to) =>
        Result(await service.Revenue(museumId, from, to, UserId, Admin));

    [AllowAnonymous, HttpGet("resources/{resourceId:int}/access")]
    public async Task<IActionResult> Access(int resourceId) => Result(await service.Access(resourceId,
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null));
}
