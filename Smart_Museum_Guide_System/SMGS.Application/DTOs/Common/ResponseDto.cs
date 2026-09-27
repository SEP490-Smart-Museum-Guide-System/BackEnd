namespace SMGS.Application.DTOs.Common;

public class ResponseDto<T>
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ResponseDto<T> Success(T data, string message = "Thành công", int statusCode = 200)
        => new() { StatusCode = statusCode, Message = message, Data = data };

    public static ResponseDto<T> Fail(string message, int statusCode = 400)
        => new() { StatusCode = statusCode, Message = message };
}