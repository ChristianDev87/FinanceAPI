using System.ComponentModel.DataAnnotations;

namespace FinanceAPI.DTOs.Categories;

public class ReorderCategoriesRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one item must be provided.")]
    public List<CategoryOrderItem> Items { get; set; } = new();

    public class CategoryOrderItem
    {
        [Range(1, int.MaxValue, ErrorMessage = "Id must be a positive integer.")]
        public int Id { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "SortOrder must be non-negative.")]
        public int SortOrder { get; set; }
    }
}
