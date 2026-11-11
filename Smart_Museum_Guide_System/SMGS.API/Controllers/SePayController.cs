using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.Interfaces;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/payments/sepay")]
[TypeFilter(typeof(CommerceExceptionFilter))]
public class SePayController(ICommerceService service) : ControllerBase
{
    [AllowAnonymous, HttpPost("webhook")]
    [Consumes("application/json")]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, cancellationToken);
        return Ok(await service.SePayWebhook(body.ToArray(),
            Request.Headers["X-SePay-Signature"].ToString(), Request.Headers["X-SePay-Timestamp"].ToString()));
    }
}
