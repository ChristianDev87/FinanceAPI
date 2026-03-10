using FinanceAPI.Models;

namespace FinanceAPI.Interfaces.Repositories;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(int id);
    Task<IEnumerable<Transaction>> GetByUserIdAsync(int userId, int? month, int? year, int? categoryId, string? type);
    Task<int> CreateAsync(Transaction transaction);
    Task UpdateAsync(Transaction transaction);
    Task DeleteAsync(int id);
}
