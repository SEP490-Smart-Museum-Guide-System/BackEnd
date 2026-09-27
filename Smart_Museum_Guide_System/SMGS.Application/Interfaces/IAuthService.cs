using SMGS.Application.DTOs.Auth.Request;
using SMGS.Application.DTOs.Auth.Response;
using SMGS.Application.DTOs.Common;

namespace SMGS.Application.Interfaces;

public interface IAuthService
{
    Task<ResponseDto<TokenResponseDto>> RegisterAsync(RegisterRequestDto dto);
    Task<ResponseDto<TokenResponseDto>> LoginAsync(LoginRequestDto dto);
}