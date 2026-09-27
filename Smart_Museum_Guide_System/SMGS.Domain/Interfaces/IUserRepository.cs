namespace SMGS.Domain.Interfaces;

public interface IUserRepository
{
    Task<UserAccountDto?> FindByEmailAsync(string email);
    Task<(UserAccountDto? User, IEnumerable<string> Errors)> CreateWithPasswordAsync(string email, string fullName, string password);
    Task<bool> CheckPasswordAsync(Guid userId, string password);
    Task<IList<string>> GetRolesAsync(Guid userId);
}

public record UserAccountDto(Guid Id, string Email, string FullName);