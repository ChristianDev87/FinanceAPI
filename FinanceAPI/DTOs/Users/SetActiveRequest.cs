using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Users;

public class SetActiveRequest
{
    [Required]
    public bool? IsActive { get; set; }
}
