using FinanceAPI.DTOs.Categories;

namespace FinanceAPI.Interfaces.Services;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllAsync(int userId, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateAsync(int userId, CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateAsync(int userId, int categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int userId, int categoryId, CancellationToken cancellationToken = default);
    Task ReorderAsync(int userId, ReorderCategoriesRequest request, CancellationToken cancellationToken = default);
}
