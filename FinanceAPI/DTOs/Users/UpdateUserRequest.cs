using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Users;

public class UpdateUserRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Admin|User)$", ErrorMessage = "Role must be 'Admin' or 'User'")]
    public string Role { get; set; } = "User";
}
