using System.Data;
using FinanceAPI.Models;

namespace FinanceAPI.Interfaces.Repositories;

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetByUserIdAsync(int userId);
    Task<Category?> GetByIdAsync(int id);
    Task<Category?> GetByUserIdAndNameAsync(int userId, string name);
    Task<int> CreateAsync(Category category);
    Task<int> CreateAsync(Category category, IDbConnection conn, IDbTransaction txn);
    Task UpdateAsync(Category category);
    Task DeleteAsync(int id);
    Task<bool> HasTransactionsAsync(int categoryId);
    Task ReorderAsync(int userId, IEnumerable<(int Id, int SortOrder)> reorderItems);
}
