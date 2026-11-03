using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SMGS.Application.Interfaces;
using SMGS.Application.DTOs.Common;

namespace SMGS.API.Controllers;

public sealed class MediaErrorFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var status = context.Exception switch {
            MediaStorageException or HttpRequestException => 503,
            FileNotFoundException => 404,
            OperationCanceledException when !context.HttpContext.RequestAborted.IsCancellationRequested => 504,
            _ => 0
        };
        if (status == 0) return;
        context.Result = new ObjectResult(ResponseDto<object>.Fail(status == 404 ? "Không tìm thấy file." : "Kho lưu trữ media chưa sẵn sàng. Vui lòng thử lại.", status)) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
