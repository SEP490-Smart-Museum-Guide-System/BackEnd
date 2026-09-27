using System.ComponentModel.DataAnnotations;
using SMGS.Application.DTOs.Auth.Request;
using SMGS.Application.DTOs.Auth.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Interfaces;

namespace SMGS.Application.Services;

public class AuthService(IUserRepository userRepo, ITokenService tokenService) : IAuthService
{
    public async Task<ResponseDto<TokenResponseDto>> RegisterAsync(RegisterRequestDto dto)
    {
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), null, validateAllProperties: true))
            return ResponseDto<TokenResponseDto>.Fail("Thông tin đăng ký không hợp lệ.", 400);

        var email = dto.Email.Trim();
        if (await userRepo.FindByEmailAsync(email) is not null)
            return ResponseDto<TokenResponseDto>.Fail("Email đã được sử dụng.", 400);

        var (user, errors) = await userRepo.CreateWithPasswordAsync(email, dto.FullName.Trim(), dto.Password);
        if (user is null)
            return ResponseDto<TokenResponseDto>.Fail($"Đăng ký thất bại: {string.Join(", ", errors)}", 400);

        return ResponseDto<TokenResponseDto>.Success(await BuildTokenResponseAsync(user), "Đăng ký thành công.", 201);
    }

    public async Task<ResponseDto<TokenResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), null, validateAllProperties: true))
            return ResponseDto<TokenResponseDto>.Fail("Email hoặc mật khẩu không hợp lệ.", 400);

        var user = await userRepo.FindByEmailAsync(dto.Email.Trim());
        if (user is null || !await userRepo.CheckPasswordAsync(user.Id, dto.Password))
            return ResponseDto<TokenResponseDto>.Fail("Email hoặc mật khẩu không đúng.", 401);

        return ResponseDto<TokenResponseDto>.Success(await BuildTokenResponseAsync(user), "Đăng nhập thành công.");
    }

    private async Task<TokenResponseDto> BuildTokenResponseAsync(UserAccountDto user)
    {
        var roles = await userRepo.GetRolesAsync(user.Id);
        var (token, expiration) = tokenService.GenerateAccessToken(user.Id, user.Email, user.FullName, roles);
        return new TokenResponseDto
        {
            AccessToken = token,
            AccessTokenExpiration = expiration,
            Email = user.Email,
            FullName = user.FullName
        };
    }
}
