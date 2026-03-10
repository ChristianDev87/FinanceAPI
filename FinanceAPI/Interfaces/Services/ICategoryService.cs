using FinanceAPI.DTOs.Categories;

namespace FinanceAPI.Interfaces.Services;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllAsync(int userId);
    Task<CategoryDto> CreateAsync(int userId, CreateCategoryRequest request);
    Task<CategoryDto> UpdateAsync(int userId, int categoryId, UpdateCategoryRequest request);
    Task DeleteAsync(int userId, int categoryId);
    Task ReorderAsync(int userId, ReorderCategoriesRequest request);
}
