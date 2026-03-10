using Dapper;
using FinanceAPI.Database;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;

namespace FinanceAPI.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ApiKeyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ApiKey?> GetByHashAsync(string keyHash)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ApiKey>(
            "SELECT * FROM ApiKeys WHERE KeyHash = @KeyHash AND IsActive = 1", new { KeyHash = keyHash });
    }

    public async Task<IEnumerable<ApiKey>> GetByUserIdAsync(int userId)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<ApiKey>(
            "SELECT * FROM ApiKeys WHERE UserId = @UserId ORDER BY CreatedAt DESC", new { UserId = userId });
    }

    public async Task<ApiKey?> GetByIdAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ApiKey>(
            "SELECT * FROM ApiKeys WHERE Id = @Id", new { Id = id });
    }

    public async Task<int> CreateAsync(ApiKey apiKey)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleAsync<int>(
            """
            INSERT INTO ApiKeys (UserId, KeyHash, Name, IsActive, CreatedByAdminId)
            VALUES (@UserId, @KeyHash, @Name, @IsActive, @CreatedByAdminId);
            SELECT last_insert_rowid();
            """, apiKey);
    }

    public async Task DeactivateAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE ApiKeys SET IsActive = 0 WHERE Id = @Id", new { Id = id });
    }

    public async Task DeactivateAllForUserAsync(int userId)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE ApiKeys SET IsActive = 0 WHERE UserId = @UserId AND IsActive = 1",
            new { UserId = userId });
    }
}
