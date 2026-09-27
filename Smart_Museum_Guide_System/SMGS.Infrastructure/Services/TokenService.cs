using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SMGS.Application.Interfaces;

namespace SMGS.Infrastructure.Services;

public class TokenService(IOptions<JwtSettings> options) : ITokenService
{
    public (string Token, DateTime Expiration) GenerateAccessToken(
        Guid userId, string email, string fullName, IList<string> roles)
    {
        var settings = options.Value;
        var now = DateTime.UtcNow;
        var expiration = now.AddMinutes(settings.ExpireMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("fullName", fullName)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            notBefore: now, expires: expiration,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
    }
}