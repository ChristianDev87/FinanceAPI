using FinanceAPI.DTOs.Categories;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Models;

namespace FinanceAPI.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepo;

    public CategoryService(ICategoryRepository categoryRepo)
    {
        _categoryRepo = categoryRepo;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllAsync(int userId)
    {
        IEnumerable<Category> categories = await _categoryRepo.GetByUserIdAsync(userId);
        return categories.Select(MapToDto);
    }

    public async Task<CategoryDto> CreateAsync(int userId, CreateCategoryRequest request)
    {
        Category category = new Category
        {
            UserId = userId,
            Name = request.Name,
            Color = request.Color,
            Type = request.Type,
            SortOrder = request.SortOrder
        };

        int id = await _categoryRepo.CreateAsync(category);
        category.Id = id;
        return MapToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(int userId, int categoryId, UpdateCategoryRequest request)
    {
        Category category = await _categoryRepo.GetByIdAsync(categoryId)
                       ?? throw new KeyNotFoundException($"Category {categoryId} not found.");

        if (category.UserId != userId)
        {
            throw new UnauthorizedAccessException("Category does not belong to you.");
        }

        category.Name = request.Name;
        category.Color = request.Color;
        category.Type = request.Type;
        category.SortOrder = request.SortOrder;

        await _categoryRepo.UpdateAsync(category);
        return MapToDto(category);
    }

    public async Task DeleteAsync(int userId, int categoryId)
    {
        Category category = await _categoryRepo.GetByIdAsync(categoryId)
                       ?? throw new KeyNotFoundException($"Category {categoryId} not found.");

        if (category.UserId != userId)
        {
            throw new UnauthorizedAccessException("Category does not belong to you.");
        }

        if (await _categoryRepo.HasTransactionsAsync(categoryId))
        {
            throw new InvalidOperationException("Cannot delete a category that has transactions. Reassign or delete the transactions first.");
        }

        await _categoryRepo.DeleteAsync(categoryId);
    }

    public async Task ReorderAsync(int userId, ReorderCategoriesRequest request)
    {
        IEnumerable<(int Id, int SortOrder)> items = request.Items.Select(i => (i.Id, i.SortOrder));
        await _categoryRepo.ReorderAsync(userId, items);
    }

    private static CategoryDto MapToDto(Category c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Color = c.Color,
        Type = c.Type,
        SortOrder = c.SortOrder
    };
}
