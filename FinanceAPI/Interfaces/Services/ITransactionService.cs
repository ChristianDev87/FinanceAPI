using FinanceAPI.DTOs.Transactions;

namespace FinanceAPI.Interfaces.Services;

public interface ITransactionService
{
    Task<IEnumerable<TransactionDto>> GetAllAsync(int userId, int? month, int? year, int? categoryId, string? type);
    Task<TransactionDto> GetByIdAsync(int userId, int transactionId);
    Task<TransactionDto> CreateAsync(int userId, CreateTransactionRequest request);
    Task<TransactionDto> UpdateAsync(int userId, int transactionId, UpdateTransactionRequest request);
    Task DeleteAsync(int userId, int transactionId);
}
