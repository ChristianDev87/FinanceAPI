using System.Data;
using FinanceAPI.Models;

namespace FinanceAPI.Interfaces.Repositories;

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Category?> GetByUserIdAndNameAsync(int userId, string name, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Category category, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Category category, IDbConnection conn, IDbTransaction txn);
    Task UpdateAsync(Category category, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionsAsync(int categoryId, CancellationToken cancellationToken = default);
    Task ReorderAsync(int userId, IEnumerable<(int Id, int SortOrder)> reorderItems, CancellationToken cancellationToken = default);
}
