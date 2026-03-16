using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Users;

public class AdminSetPasswordRequest
{
    [Required]
    [RegularExpression(
        @"^(?=.*[a-zA-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Password must be at least 8 characters and contain at least one letter and one digit.")]
    public string NewPassword { get; set; } = string.Empty;
}
