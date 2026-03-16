namespace FinanceAPI.Models;

public class Transaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty; // "income" | "expense"
    public int? CategoryId { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
