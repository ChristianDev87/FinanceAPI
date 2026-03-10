using FinanceAPI.Models;

namespace FinanceAPI.Interfaces.Repositories;

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetByUserIdAsync(int userId);
    Task<Category?> GetByIdAsync(int id);
    Task<int> CreateAsync(Category category);
    Task UpdateAsync(Category category);
    Task DeleteAsync(int id);
    Task<bool> HasTransactionsAsync(int categoryId);
    Task ReorderAsync(int userId, IEnumerable<(int Id, int SortOrder)> reorderItems);
}
