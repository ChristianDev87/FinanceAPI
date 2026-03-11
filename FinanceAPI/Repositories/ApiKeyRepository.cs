using Dapper;
using FinanceAPI.Database;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;

namespace FinanceAPI.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISqlDialect _dialect;

    public ApiKeyRepository(IDbConnectionFactory connectionFactory, ISqlDialect dialect)
    {
        _connectionFactory = connectionFactory;
        _dialect = dialect;
    }

    public async Task<ApiKey?> GetByHashAsync(string keyHash)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ApiKey>(
            "SELECT * FROM ApiKeys WHERE KeyHash = @KeyHash AND IsActive = @IsActive",
            new { KeyHash = keyHash, IsActive = true });
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
        return await _dialect.InsertAsync(conn,
            "INSERT INTO ApiKeys (UserId, KeyHash, Name, IsActive, CreatedByAdminId) VALUES (@UserId, @KeyHash, @Name, @IsActive, @CreatedByAdminId)",
            apiKey);
    }

    public async Task DeactivateAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE ApiKeys SET IsActive = @IsActive WHERE Id = @Id",
            new { Id = id, IsActive = false });
    }

    public async Task DeactivateAllForUserAsync(int userId)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE ApiKeys SET IsActive = @IsActive WHERE UserId = @UserId AND IsActive = @CurrentIsActive",
            new { UserId = userId, IsActive = false, CurrentIsActive = true });
    }
}
