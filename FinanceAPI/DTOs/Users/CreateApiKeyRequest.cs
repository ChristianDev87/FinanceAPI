using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Users;

public class CreateApiKeyRequest
{
    [Required]
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;
}
