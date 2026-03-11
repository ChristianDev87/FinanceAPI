using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Users;

public class AdminSetPasswordRequest
{
    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}
