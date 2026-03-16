using System.Data;
using Dapper;
using FinanceAPI.Database;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;

namespace FinanceAPI.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISqlDialect _dialect;

    public CategoryRepository(IDbConnectionFactory connectionFactory, ISqlDialect dialect)
    {
        _connectionFactory = connectionFactory;
        _dialect = dialect;
    }

    public async Task<IEnumerable<Category>> GetByUserIdAsync(int userId)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<Category>(
            "SELECT * FROM Categories WHERE UserId = @UserId ORDER BY SortOrder ASC, Id ASC",
            new { UserId = userId });
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Category>(
            "SELECT * FROM Categories WHERE Id = @Id", new { Id = id });
    }

    public async Task<int> CreateAsync(Category category)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await _dialect.InsertAsync(conn,
            "INSERT INTO Categories (UserId, Name, Color, Type, SortOrder) VALUES (@UserId, @Name, @Color, @Type, @SortOrder)",
            category);
    }

    public async Task UpdateAsync(Category category)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Categories SET Name = @Name, Color = @Color, Type = @Type, SortOrder = @SortOrder WHERE Id = @Id",
            category);
    }

    public async Task DeleteAsync(int id)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Categories WHERE Id = @Id", new { Id = id });
    }

    public async Task<bool> HasTransactionsAsync(int categoryId)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        int count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM Transactions WHERE CategoryId = @CategoryId", new { CategoryId = categoryId });
        return count > 0;
    }

    public async Task ReorderAsync(int userId, IEnumerable<(int Id, int SortOrder)> reorderItems)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        foreach ((int id, int sortOrder) in reorderItems)
        {
            await conn.ExecuteAsync(
                "UPDATE Categories SET SortOrder = @SortOrder WHERE Id = @Id AND UserId = @UserId",
                new { SortOrder = sortOrder, Id = id, UserId = userId });
        }
    }
}
