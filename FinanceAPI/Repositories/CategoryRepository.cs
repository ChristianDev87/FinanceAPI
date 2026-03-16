using System.Data;
using System.Text;
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

    public async Task<IEnumerable<Category>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<Category>(
            new CommandDefinition(
                "SELECT * FROM Categories WHERE UserId = @UserId ORDER BY SortOrder ASC, Id ASC",
                new { UserId = userId },
                cancellationToken: cancellationToken));
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Category>(
            new CommandDefinition(
                "SELECT * FROM Categories WHERE Id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<Category?> GetByUserIdAndNameAsync(int userId, string name, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Category>(
            new CommandDefinition(
                $"SELECT * FROM Categories WHERE UserId = @UserId AND {_dialect.CaseInsensitiveEqual("Name", "@Name")}",
                new { UserId = userId, Name = name },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(Category category, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await _dialect.InsertAsync(conn,
            "INSERT INTO Categories (UserId, Name, Color, Type, SortOrder) VALUES (@UserId, @Name, @Color, @Type, @SortOrder)",
            category, cancellationToken: cancellationToken);
    }

    public Task<int> CreateAsync(Category category, IDbConnection conn, IDbTransaction txn)
        => _dialect.InsertAsync(conn,
            "INSERT INTO Categories (UserId, Name, Color, Type, SortOrder) VALUES (@UserId, @Name, @Color, @Type, @SortOrder)",
            category, txn);

    public async Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Categories SET Name = @Name, Color = @Color, Type = @Type, SortOrder = @SortOrder WHERE Id = @Id",
                category,
                cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Categories WHERE Id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> HasTransactionsAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        int count = await conn.QuerySingleAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Transactions WHERE CategoryId = @CategoryId",
                new { CategoryId = categoryId },
                cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task ReorderAsync(int userId, IEnumerable<(int Id, int SortOrder)> reorderItems, CancellationToken cancellationToken = default)
    {
        List<(int Id, int SortOrder)> items = reorderItems.ToList();
        if (items.Count == 0)
        {
            return;
        }

        using IDbConnection conn = _connectionFactory.CreateConnection();

        DynamicParameters parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        StringBuilder caseExpr = new StringBuilder("CASE Id");
        List<string> inParams = new List<string>();

        for (int i = 0; i < items.Count; i++)
        {
            string idParam = $"Id{i}";
            string sortParam = $"Sort{i}";
            caseExpr.Append($" WHEN @{idParam} THEN @{sortParam}");
            inParams.Add($"@{idParam}");
            parameters.Add(idParam, items[i].Id);
            parameters.Add(sortParam, items[i].SortOrder);
        }

        caseExpr.Append(" END");

        string sql = $"UPDATE Categories SET SortOrder = {caseExpr} WHERE UserId = @UserId AND Id IN ({string.Join(", ", inParams)})";

        await conn.ExecuteAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}
