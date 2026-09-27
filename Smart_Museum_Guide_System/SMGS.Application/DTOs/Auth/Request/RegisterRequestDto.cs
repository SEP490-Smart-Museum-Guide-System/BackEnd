using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Auth.Request;

public class RegisterRequestDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(1024, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}
