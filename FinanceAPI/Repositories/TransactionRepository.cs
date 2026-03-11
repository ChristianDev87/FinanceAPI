using Dapper;
using FinanceAPI.Database;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;

namespace FinanceAPI.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISqlDialect _dialect;

    public TransactionRepository(IDbConnectionFactory connectionFactory, ISqlDialect dialect)
    {
        _connectionFactory = connectionFactory;
        _dialect = dialect;
    }

    public async Task<Transaction?> GetByIdAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Transaction>(
            "SELECT * FROM Transactions WHERE Id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<Transaction>> GetByUserIdAsync(
        int userId, int? month, int? year, int? categoryId, string? type)
    {
        using var conn = _connectionFactory.CreateConnection();

        var sql = "SELECT * FROM Transactions WHERE UserId = @UserId";
        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        if (month.HasValue)
        {
            sql += $" AND {_dialect.Month("Date")} = @Month";
            parameters.Add("Month", month.Value);
        }
        if (year.HasValue)
        {
            sql += $" AND {_dialect.Year("Date")} = @Year";
            parameters.Add("Year", year.Value);
        }
        if (categoryId.HasValue)
        {
            sql += " AND CategoryId = @CategoryId";
            parameters.Add("CategoryId", categoryId.Value);
        }
        if (!string.IsNullOrEmpty(type))
        {
            sql += " AND Type = @Type";
            parameters.Add("Type", type);
        }

        sql += " ORDER BY Date DESC, Id DESC";
        return await conn.QueryAsync<Transaction>(sql, parameters);
    }

    public async Task<int> CreateAsync(Transaction transaction)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await _dialect.InsertAsync(conn,
            "INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date, Description) VALUES (@UserId, @Amount, @Type, @CategoryId, @Date, @Description)",
            transaction);
    }

    public async Task UpdateAsync(Transaction transaction)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE Transactions
            SET Amount = @Amount, Type = @Type, CategoryId = @CategoryId,
                Date = @Date, Description = @Description
            WHERE Id = @Id
            """, transaction);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Transactions WHERE Id = @Id", new { Id = id });
    }
}
