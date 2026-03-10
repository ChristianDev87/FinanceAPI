namespace FinanceAPI.Models;

public class Category
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#1abc9c";
    public string Type { get; set; } = string.Empty; // "income" | "expense"
    public int SortOrder { get; set; }
}
