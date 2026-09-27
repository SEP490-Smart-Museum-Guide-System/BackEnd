using System.ComponentModel.DataAnnotations;

namespace SMGS.Application.DTOs.Auth.Request;

public class LoginRequestDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(1024)]
    public string Password { get; set; } = string.Empty;
}