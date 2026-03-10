using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Categories;

public class CreateCategoryRequest
{
    [Required]
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = "#1abc9c";

    [Required]
    [RegularExpression("^(income|expense)$", ErrorMessage = "Type must be 'income' or 'expense'")]
    public string Type { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;
}
