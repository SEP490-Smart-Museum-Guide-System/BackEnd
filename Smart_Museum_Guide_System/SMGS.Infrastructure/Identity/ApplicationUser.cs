using Microsoft.AspNetCore.Identity;

namespace SMGS.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string PreferredLanguage { get; set; } = "vi";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
