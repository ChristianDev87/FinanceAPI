using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Profile;

public class UpdateProfileRequest
{
    [Required, MinLength(3)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
