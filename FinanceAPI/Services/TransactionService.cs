using System.Globalization;
using FinanceAPI.DTOs.Transactions;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Models;

namespace FinanceAPI.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly ICategoryRepository _categoryRepo;

    public TransactionService(ITransactionRepository transactionRepo, ICategoryRepository categoryRepo)
    {
        _transactionRepo = transactionRepo;
        _categoryRepo = categoryRepo;
    }

    public async Task<IEnumerable<TransactionDto>> GetAllAsync(
        int userId, int? month, int? year, int? categoryId, string? type, CancellationToken cancellationToken = default)
    {
        IEnumerable<Transaction> transactions = await _transactionRepo.GetByUserIdAsync(userId, month, year, categoryId, type, cancellationToken);
        Dictionary<int, Category> categories = (await _categoryRepo.GetByUserIdAsync(userId, cancellationToken)).ToDictionary(c => c.Id);

        return transactions.Select(t => MapToDto(t, categories));
    }

    public async Task<TransactionDto> GetByIdAsync(int userId, int transactionId, CancellationToken cancellationToken = default)
    {
        Transaction transaction = await _transactionRepo.GetByIdAsync(transactionId, cancellationToken)
                          ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");

        if (transaction.UserId != userId)
        {
            throw new UnauthorizedAccessException("Transaction does not belong to you.");
        }

        Category? category = transaction.CategoryId.HasValue
            ? await _categoryRepo.GetByIdAsync(transaction.CategoryId.Value, cancellationToken)
            : null;

        return MapToDto(transaction, category);
    }

    public async Task<TransactionDto> CreateAsync(int userId, CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        Category? category = null;
        if (request.CategoryId.HasValue)
        {
            category = await _categoryRepo.GetByIdAsync(request.CategoryId.Value, cancellationToken)
                      ?? throw new KeyNotFoundException($"Category {request.CategoryId} not found.");
            if (category.UserId != userId)
            {
                throw new UnauthorizedAccessException("Category does not belong to you.");
            }
            if (!string.Equals(category.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Category type '{category.Type}' does not match transaction type '{request.Type}'.");
            }
        }

        Transaction transaction = new Transaction
        {
            UserId = userId,
            Amount = request.Amount,
            Type = request.Type,
            CategoryId = request.CategoryId,
            Date = DateOnly.ParseExact(request.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Description = request.Description
        };

        int id = await _transactionRepo.CreateAsync(transaction, cancellationToken);
        transaction.Id = id;

        return MapToDto(transaction, category);
    }

    public async Task<TransactionDto> UpdateAsync(int userId, int transactionId, UpdateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        Transaction transaction = await _transactionRepo.GetByIdAsync(transactionId, cancellationToken)
                          ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");

        if (transaction.UserId != userId)
        {
            throw new UnauthorizedAccessException("Transaction does not belong to you.");
        }

        Category? category = null;
        if (request.CategoryId.HasValue)
        {
            category = await _categoryRepo.GetByIdAsync(request.CategoryId.Value, cancellationToken)
                      ?? throw new KeyNotFoundException($"Category {request.CategoryId} not found.");
            if (category.UserId != userId)
            {
                throw new UnauthorizedAccessException("Category does not belong to you.");
            }
            if (!string.Equals(category.Type, request.Type, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Category type '{category.Type}' does not match transaction type '{request.Type}'.");
            }
        }

        transaction.Amount = request.Amount;
        transaction.Type = request.Type;
        transaction.CategoryId = request.CategoryId;
        transaction.Date = DateOnly.ParseExact(request.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        transaction.Description = request.Description;

        await _transactionRepo.UpdateAsync(transaction, cancellationToken);

        return MapToDto(transaction, category);
    }

    public async Task DeleteAsync(int userId, int transactionId, CancellationToken cancellationToken = default)
    {
        Transaction transaction = await _transactionRepo.GetByIdAsync(transactionId, cancellationToken)
                          ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");

        if (transaction.UserId != userId)
        {
            throw new UnauthorizedAccessException("Transaction does not belong to you.");
        }

        await _transactionRepo.DeleteAsync(transactionId, cancellationToken);
    }

    private static TransactionDto MapToDto(Transaction t, Category? category) => new()
    {
        Id = t.Id,
        Amount = t.Amount,
        Type = t.Type,
        CategoryId = t.CategoryId,
        CategoryName = category?.Name,
        Date = t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Description = t.Description,
        CreatedAt = t.CreatedAt
    };

    private static TransactionDto MapToDto(Transaction t, Dictionary<int, Category> categories) => new()
    {
        Id = t.Id,
        Amount = t.Amount,
        Type = t.Type,
        CategoryId = t.CategoryId,
        CategoryName = t.CategoryId.HasValue && categories.TryGetValue(t.CategoryId.Value, out Category? cat)
            ? cat.Name : null,
        Date = t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Description = t.Description,
        CreatedAt = t.CreatedAt
    };
}
