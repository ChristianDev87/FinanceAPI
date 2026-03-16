using FinanceAPI.DTOs.Transactions;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;
using FinanceAPI.Services;
using Moq;

namespace FinanceAPI.Tests.Unit;

public class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _txRepo = new();
    private readonly Mock<ICategoryRepository> _catRepo = new();
    private readonly TransactionService _sut;

    public TransactionServiceTests()
    {
        _sut = new TransactionService(_txRepo.Object, _catRepo.Object);
        // Default: no categories for the user
        _catRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<Category>());
    }

    private static Transaction MakeTx(int id, int userId, decimal amount = 50m) => new()
    {
        Id = id,
        UserId = userId,
        Amount = amount,
        Type = "expense",
        Date = "2026-01-01"
    };

    private static Category MakeCat(int id, int userId) => new()
    {
        Id = id,
        UserId = userId,
        Name = "Food",
        Color = "#fff",
        Type = "expense",
        SortOrder = 0
    };

    // ── GetAll ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsMappedTransactions()
    {
        _txRepo.Setup(r => r.GetByUserIdAsync(1, null, null, null, null))
               .ReturnsAsync(new[] { MakeTx(1, 1, 100m), MakeTx(2, 1, 200m) });

        List<TransactionDto> result = (await _sut.GetAllAsync(1, null, null, null, null)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(100m, result[0].Amount);
        Assert.Equal(200m, result[1].Amount);
    }

    [Fact]
    public async Task GetAllAsync_WithFilters_PassesFiltersToRepository()
    {
        _txRepo.Setup(r => r.GetByUserIdAsync(1, 3, 2026, null, "expense"))
               .ReturnsAsync(new[] { MakeTx(1, 1) });

        List<TransactionDto> result = (await _sut.GetAllAsync(1, 3, 2026, null, "expense")).ToList();

        Assert.Single(result);
        _txRepo.Verify(r => r.GetByUserIdAsync(1, 3, 2026, null, "expense"), Times.Once);
    }

    // ── GetById ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_OwnTransaction_ReturnsDto()
    {
        _txRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeTx(1, 1, 150m));

        TransactionDto result = await _sut.GetByIdAsync(1, 1);

        Assert.Equal(1, result.Id);
        Assert.Equal(150m, result.Amount);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _txRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Transaction?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetByIdAsync(1, 99));
    }

    [Fact]
    public async Task GetByIdAsync_OtherUsersTransaction_ThrowsUnauthorizedAccessException()
    {
        _txRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeTx(1, userId: 99));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.GetByIdAsync(1, 1));
    }

    // ── Create ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_NoCategory_CreatesSuccessfully()
    {
        _txRepo.Setup(r => r.CreateAsync(It.IsAny<Transaction>())).ReturnsAsync(10);

        TransactionDto result = await _sut.CreateAsync(1, new CreateTransactionRequest
        {
            Amount = 75m,
            Type = "expense",
            Date = "2026-03-01",
            CategoryId = null
        });

        Assert.Equal(10, result.Id);
        Assert.Equal(75m, result.Amount);
        Assert.Null(result.CategoryId);
    }

    [Fact]
    public async Task CreateAsync_ValidCategory_SetsCategoryName()
    {
        _catRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(MakeCat(3, 1));
        _catRepo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(new[] { MakeCat(3, 1) });
        _txRepo.Setup(r => r.CreateAsync(It.IsAny<Transaction>())).ReturnsAsync(11);

        TransactionDto result = await _sut.CreateAsync(1, new CreateTransactionRequest
        {
            Amount = 30m,
            Type = "expense",
            Date = "2026-03-02",
            CategoryId = 3
        });

        Assert.Equal(11, result.Id);
        Assert.Equal("Food", result.CategoryName);
    }

    [Fact]
    public async Task CreateAsync_CategoryNotFound_ThrowsKeyNotFoundException()
    {
        _catRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Category?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.CreateAsync(1, new CreateTransactionRequest
            {
                Amount = 10m,
                Type = "expense",
                Date = "2026-01-01",
                CategoryId = 99
            }));
    }

    [Fact]
    public async Task CreateAsync_OtherUsersCategory_ThrowsUnauthorizedAccessException()
    {
        _catRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(MakeCat(3, userId: 99));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.CreateAsync(1, new CreateTransactionRequest
            {
                Amount = 10m,
                Type = "expense",
                Date = "2026-01-01",
                CategoryId = 3
            }));
    }

    // ── Update ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_OwnTransaction_ReturnsUpdatedDto()
    {
        _txRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeTx(1, 1, 50m));

        TransactionDto result = await _sut.UpdateAsync(1, 1, new UpdateTransactionRequest
        {
            Amount = 99m,
            Type = "income",
            Date = "2026-06-01",
            CategoryId = null
        });

        Assert.Equal(99m, result.Amount);
        Assert.Equal("income", result.Type);
        _txRepo.Verify(r => r.UpdateAsync(It.IsAny<Transaction>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _txRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Transaction?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.UpdateAsync(1, 99, new UpdateTransactionRequest
            {
                Amount = 10m,
                Type = "expense",
                Date = "2026-01-01"
            }));
    }

    [Fact]
    public async Task UpdateAsync_OtherUsersTransaction_ThrowsUnauthorizedAccessException()
    {
        _txRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeTx(1, userId: 99));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.UpdateAsync(1, 1, new UpdateTransactionRequest
            {
                Amount = 10m,
                Type = "expense",
                Date = "2026-01-01"
            }));
    }

    // ── Delete ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_OwnTransaction_Deletes()
    {
        _txRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeTx(1, 1));

        await _sut.DeleteAsync(1, 1);

        _txRepo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _txRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Transaction?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.DeleteAsync(1, 99));
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersTransaction_ThrowsUnauthorizedAccessException()
    {
        _txRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeTx(1, userId: 99));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.DeleteAsync(1, 1));
    }
}
