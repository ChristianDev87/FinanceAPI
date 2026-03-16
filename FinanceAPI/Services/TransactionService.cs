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
        int userId, int? month, int? year, int? categoryId, string? type)
    {
        IEnumerable<Transaction> transactions = await _transactionRepo.GetByUserIdAsync(userId, month, year, categoryId, type);
        Dictionary<int, Category> categories = (await _categoryRepo.GetByUserIdAsync(userId)).ToDictionary(c => c.Id);

        return transactions.Select(t => MapToDto(t, categories));
    }

    public async Task<TransactionDto> GetByIdAsync(int userId, int transactionId)
    {
        Transaction transaction = await _transactionRepo.GetByIdAsync(transactionId)
                          ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");

        if (transaction.UserId != userId)
        {
            throw new UnauthorizedAccessException("Transaction does not belong to you.");
        }

        Dictionary<int, Category> categories = (await _categoryRepo.GetByUserIdAsync(userId)).ToDictionary(c => c.Id);
        return MapToDto(transaction, categories);
    }

    public async Task<TransactionDto> CreateAsync(int userId, CreateTransactionRequest request)
    {
        if (request.CategoryId.HasValue)
        {
            Category cat = await _categoryRepo.GetByIdAsync(request.CategoryId.Value)
                      ?? throw new KeyNotFoundException($"Category {request.CategoryId} not found.");
            if (cat.UserId != userId)
            {
                throw new UnauthorizedAccessException("Category does not belong to you.");
            }
        }

        Transaction transaction = new Transaction
        {
            UserId = userId,
            Amount = request.Amount,
            Type = request.Type,
            CategoryId = request.CategoryId,
            Date = request.Date,
            Description = request.Description
        };

        int id = await _transactionRepo.CreateAsync(transaction);
        transaction.Id = id;

        Dictionary<int, Category> categories = (await _categoryRepo.GetByUserIdAsync(userId)).ToDictionary(c => c.Id);
        return MapToDto(transaction, categories);
    }

    public async Task<TransactionDto> UpdateAsync(int userId, int transactionId, UpdateTransactionRequest request)
    {
        Transaction transaction = await _transactionRepo.GetByIdAsync(transactionId)
                          ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");

        if (transaction.UserId != userId)
        {
            throw new UnauthorizedAccessException("Transaction does not belong to you.");
        }

        if (request.CategoryId.HasValue)
        {
            Category cat = await _categoryRepo.GetByIdAsync(request.CategoryId.Value)
                      ?? throw new KeyNotFoundException($"Category {request.CategoryId} not found.");
            if (cat.UserId != userId)
            {
                throw new UnauthorizedAccessException("Category does not belong to you.");
            }
        }

        transaction.Amount = request.Amount;
        transaction.Type = request.Type;
        transaction.CategoryId = request.CategoryId;
        transaction.Date = request.Date;
        transaction.Description = request.Description;

        await _transactionRepo.UpdateAsync(transaction);

        Dictionary<int, Category> categories = (await _categoryRepo.GetByUserIdAsync(userId)).ToDictionary(c => c.Id);
        return MapToDto(transaction, categories);
    }

    public async Task DeleteAsync(int userId, int transactionId)
    {
        Transaction transaction = await _transactionRepo.GetByIdAsync(transactionId)
                          ?? throw new KeyNotFoundException($"Transaction {transactionId} not found.");

        if (transaction.UserId != userId)
        {
            throw new UnauthorizedAccessException("Transaction does not belong to you.");
        }

        await _transactionRepo.DeleteAsync(transactionId);
    }

    private static TransactionDto MapToDto(Transaction t, Dictionary<int, Models.Category> categories) => new()
    {
        Id = t.Id,
        Amount = t.Amount,
        Type = t.Type,
        CategoryId = t.CategoryId,
        CategoryName = t.CategoryId.HasValue && categories.TryGetValue(t.CategoryId.Value, out Category? cat)
            ? cat.Name : null,
        Date = t.Date,
        Description = t.Description,
        CreatedAt = t.CreatedAt
    };
}
