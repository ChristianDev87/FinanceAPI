using FinanceAPI.DTOs.Categories;
using FinanceAPI.Exceptions;
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

    public async Task<IEnumerable<CategoryDto>> GetAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        IEnumerable<Category> categories = await _categoryRepo.GetByUserIdAsync(userId, cancellationToken);
        return categories.Select(MapToDto);
    }

    public async Task<CategoryDto> CreateAsync(int userId, CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (await _categoryRepo.GetByUserIdAndNameAsync(userId, request.Name, cancellationToken) is not null)
        {
            throw new ConflictException($"A category named '{request.Name}' already exists.");
        }

        Category category = new Category
        {
            UserId = userId,
            Name = request.Name,
            Color = request.Color,
            Type = request.Type,
            SortOrder = request.SortOrder
        };

        int id = await _categoryRepo.CreateAsync(category, cancellationToken);
        category.Id = id;
        return MapToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(int userId, int categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        Category category = await _categoryRepo.GetByIdAsync(categoryId, cancellationToken)
                       ?? throw new NotFoundException($"Category {categoryId} not found.");

        if (category.UserId != userId)
        {
            throw new ForbiddenException("Category does not belong to you.");
        }

        if (!string.Equals(category.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            Category? duplicate = await _categoryRepo.GetByUserIdAndNameAsync(userId, request.Name, cancellationToken);
            if (duplicate is not null && duplicate.Id != categoryId)
            {
                throw new ConflictException($"A category named '{request.Name}' already exists.");
            }
        }

        // Note: the type-change check and the subsequent write are not atomic. A concurrent
        // transaction insert on this category between this check and the update could create
        // a type mismatch. For a single-user personal finance app this race is acceptable;
        // enforce atomically at the DB layer if stricter guarantees are required.
        if (!string.Equals(category.Type, request.Type, StringComparison.OrdinalIgnoreCase)
            && await _categoryRepo.HasTransactionsAsync(categoryId, cancellationToken))
        {
            throw new InvalidOperationException("Cannot change the type of a category that has existing transactions.");
        }

        category.Name = request.Name;
        category.Color = request.Color;
        category.Type = request.Type;
        category.SortOrder = request.SortOrder;

        await _categoryRepo.UpdateAsync(category, cancellationToken);
        return MapToDto(category);
    }

    public async Task DeleteAsync(int userId, int categoryId, CancellationToken cancellationToken = default)
    {
        Category category = await _categoryRepo.GetByIdAsync(categoryId, cancellationToken)
                       ?? throw new NotFoundException($"Category {categoryId} not found.");

        if (category.UserId != userId)
        {
            throw new ForbiddenException("Category does not belong to you.");
        }

        if (await _categoryRepo.HasTransactionsAsync(categoryId, cancellationToken))
        {
            throw new InvalidOperationException("Cannot delete a category that has transactions. Reassign or delete the transactions first.");
        }

        await _categoryRepo.DeleteAsync(categoryId, cancellationToken);
    }

    public async Task ReorderAsync(int userId, ReorderCategoriesRequest request, CancellationToken cancellationToken = default)
    {
        HashSet<int> userCategoryIds = new HashSet<int>(
            (await _categoryRepo.GetByUserIdAsync(userId, cancellationToken)).Select(c => c.Id));

        foreach (ReorderCategoriesRequest.CategoryOrderItem item in request.Items)
        {
            if (!userCategoryIds.Contains(item.Id))
            {
                throw new ForbiddenException($"Category {item.Id} does not belong to you.");
            }
        }

        IEnumerable<(int Id, int SortOrder)> items = request.Items.Select(i => (i.Id, i.SortOrder));
        await _categoryRepo.ReorderAsync(userId, items, cancellationToken);
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
