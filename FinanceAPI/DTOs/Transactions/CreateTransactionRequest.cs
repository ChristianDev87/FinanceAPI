using System.ComponentModel.DataAnnotations;
using FinanceAPI.Validation;

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
    [ValidDate]
    public string Date { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
