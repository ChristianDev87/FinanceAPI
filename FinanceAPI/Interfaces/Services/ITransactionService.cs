using FinanceAPI.DTOs.Transactions;

namespace FinanceAPI.Interfaces.Services;

public interface ITransactionService
{
    Task<IEnumerable<TransactionDto>> GetAllAsync(int userId, int? month, int? year, int? categoryId, string? type, CancellationToken cancellationToken = default);
    Task<TransactionDto> GetByIdAsync(int userId, int transactionId, CancellationToken cancellationToken = default);
    Task<TransactionDto> CreateAsync(int userId, CreateTransactionRequest request, CancellationToken cancellationToken = default);
    Task<TransactionDto> UpdateAsync(int userId, int transactionId, UpdateTransactionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int userId, int transactionId, CancellationToken cancellationToken = default);
}
