using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Categories;

public class ReorderCategoriesRequest
{
    [Required]
    public List<CategoryOrderItem> Items { get; set; } = new();

    public class CategoryOrderItem
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }
}
