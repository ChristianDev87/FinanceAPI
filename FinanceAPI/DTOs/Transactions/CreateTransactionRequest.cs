using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Transactions;

public class CreateTransactionRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression("^(income|expense)$", ErrorMessage = "Type must be 'income' or 'expense'")]
    public string Type { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    [Required]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Date must be in YYYY-MM-DD format")]
    public string Date { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
