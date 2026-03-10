namespace FinanceAPI.DTOs.Statistics;

public class CategoryStatDto
{
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = "Uncategorized";
    public string Color { get; set; } = "#95a5a6";
    public string Type { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Count { get; set; }
}
