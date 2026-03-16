using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Profile;

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [RegularExpression(
        @"^(?=.*[a-zA-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Password must be at least 8 characters and contain at least one letter and one digit.")]
    public string NewPassword { get; set; } = string.Empty;
}
