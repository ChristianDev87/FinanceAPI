using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Profile;

public class UpdateProfileRequest
{
    [Required, MinLength(3), MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;
}
