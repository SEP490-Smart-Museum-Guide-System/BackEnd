using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Account;

public class CreateStaffAccountRequestDto
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    public List<string> Roles { get; set; } = ["Museum Staff"];
}

public class UpdateAccountRequestDto
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [MinLength(6), MaxLength(100)]
    public string? NewPassword { get; set; }
}

public class SetAccountStatusRequestDto
{
    public bool IsActive { get; set; }
}

public class AssignRolesRequestDto
{
    [Required]
    public List<string> Roles { get; set; } = [];
}

public class ScopeAssignmentRequestDto
{
    [Range(1, int.MaxValue)]
    public int MuseumId { get; set; }

    public int? GalleryId { get; set; }

    public int? ArtifactId { get; set; }
}

public class ScopeAssignmentResponseDto
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public int MuseumId { get; set; }
    public string MuseumName { get; set; } = string.Empty;
    public int? GalleryId { get; set; }
    public string? GalleryName { get; set; }
    public int? ArtifactId { get; set; }
    public string? ArtifactName { get; set; }
    public string ScopeLevel { get; set; } = "museum";
    public DateTime AssignedAt { get; set; }
}

public class AccountResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime CreatedAt { get; set; }
    public IList<string> Roles { get; set; } = [];
    public List<ScopeAssignmentResponseDto> Assignments { get; set; } = [];
}

