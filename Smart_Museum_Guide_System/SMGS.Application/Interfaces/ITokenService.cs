namespace SMGS.Application.Interfaces;

public interface ITokenService
{
    (string Token, DateTime Expiration) GenerateAccessToken(
        Guid userId, string email, string fullName, IList<string> roles);
}