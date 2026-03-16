using FinanceAPI.DTOs.Categories;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;
using FinanceAPI.Services;
using Moq;

namespace FinanceAPI.Tests.Unit;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _repo = new();
    private readonly CategoryService _sut;

    public CategoryServiceTests()
    {
        _sut = new CategoryService(_repo.Object);
    }

    private static Category MakeCat(int id, int userId, string name = "Food") => new()
    {
        Id = id,
        UserId = userId,
        Name = name,
        Color = "#fff",
        Type = "expense",
        SortOrder = 0
    };

    // ── GetAll ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsUserCategories()
    {
        _repo.Setup(r => r.GetByUserIdAsync(1))
             .ReturnsAsync(new[] { MakeCat(1, 1), MakeCat(2, 1, "Transport") });

        List<CategoryDto> result = (await _sut.GetAllAsync(1)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("Food", result[0].Name);
        Assert.Equal("Transport", result[1].Name);
    }

    [Fact]
    public async Task GetAllAsync_NoCategories_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(Array.Empty<Category>());

        IEnumerable<CategoryDto> result = await _sut.GetAllAsync(1);

        Assert.Empty(result);
    }

    // ── Create ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsDtoWithNewId()
    {
        _repo.Setup(r => r.CreateAsync(It.IsAny<Category>())).ReturnsAsync(5);

        CategoryDto result = await _sut.CreateAsync(1, new CreateCategoryRequest
        {
            Name = "Shopping",
            Color = "#ff0000",
            Type = "expense",
            SortOrder = 3
        });

        Assert.Equal(5, result.Id);
        Assert.Equal("Shopping", result.Name);
        Assert.Equal("#ff0000", result.Color);
        Assert.Equal("expense", result.Type);
    }

    // ── Update ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_OwnCategory_ReturnsUpdatedDto()
    {
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeCat(1, 1));

        CategoryDto result = await _sut.UpdateAsync(1, 1, new UpdateCategoryRequest
        {
            Name = "NewName",
            Color = "#aabbcc",
            Type = "income",
            SortOrder = 2
        });

        Assert.Equal("NewName", result.Name);
        Assert.Equal("income", result.Type);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Category>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_CategoryNotFound_ThrowsKeyNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Category?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.UpdateAsync(1, 99, new UpdateCategoryRequest
            {
                Name = "X",
                Color = "#fff",
                Type = "expense",
                SortOrder = 0
            }));
    }

    [Fact]
    public async Task UpdateAsync_OtherUsersCategory_ThrowsUnauthorizedAccessException()
    {
        _repo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeCat(2, 99)); // belongs to user 99

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.UpdateAsync(1, 2, new UpdateCategoryRequest
            {
                Name = "X",
                Color = "#fff",
                Type = "expense",
                SortOrder = 0
            }));
    }

    [Fact]
    public async Task UpdateAsync_TypeChangedWithExistingTransactions_ThrowsInvalidOperationException()
    {
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeCat(1, 1)); // Type = "expense"
        _repo.Setup(r => r.HasTransactionsAsync(1)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateAsync(1, 1, new UpdateCategoryRequest
            {
                Name = "Food",
                Color = "#ff0000",
                Type = "income",
                SortOrder = 0
            }));
    }

    // ── Delete ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_OwnEmptyCategory_Deletes()
    {
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeCat(1, 1));
        _repo.Setup(r => r.HasTransactionsAsync(1)).ReturnsAsync(false);

        await _sut.DeleteAsync(1, 1);

        _repo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsKeyNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Category?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.DeleteAsync(1, 99));
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersCategory_ThrowsUnauthorizedAccessException()
    {
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeCat(1, 99));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.DeleteAsync(1, 1));
    }

    [Fact]
    public async Task DeleteAsync_CategoryHasTransactions_ThrowsInvalidOperationException()
    {
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeCat(1, 1));
        _repo.Setup(r => r.HasTransactionsAsync(1)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAsync(1, 1));
        _repo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    // ── Reorder ───────────────────────────────────────────────────

    [Fact]
    public async Task ReorderAsync_ValidRequest_CallsRepositoryReorder()
    {
        _repo.Setup(r => r.GetByUserIdAsync(1))
             .ReturnsAsync(new[] { MakeCat(1, 1), MakeCat(2, 1, "Transport") });

        ReorderCategoriesRequest request = new ReorderCategoriesRequest
        {
            Items = new List<ReorderCategoriesRequest.CategoryOrderItem>
            {
                new() { Id = 1, SortOrder = 0 },
                new() { Id = 2, SortOrder = 1 },
            }
        };

        await _sut.ReorderAsync(1, request);

        _repo.Verify(r => r.ReorderAsync(1, It.IsAny<IEnumerable<(int, int)>>()), Times.Once);
    }
}
