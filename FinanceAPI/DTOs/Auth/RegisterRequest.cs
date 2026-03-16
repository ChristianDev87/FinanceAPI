using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Auth;

public class RegisterRequest
{
    [Required]
    [MinLength(3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(
        @"^(?=.*[a-zA-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Password must be at least 8 characters and contain at least one letter and one digit.")]
    public string Password { get; set; } = string.Empty;
}
