using System.Data;
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

    public async Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ApiKey>(
            new CommandDefinition(
                "SELECT * FROM ApiKeys WHERE KeyHash = @KeyHash AND IsActive = @IsActive",
                new { KeyHash = keyHash, IsActive = true },
                cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<ApiKey>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<ApiKey>(
            new CommandDefinition(
                "SELECT * FROM ApiKeys WHERE UserId = @UserId ORDER BY CreatedAt DESC",
                new { UserId = userId },
                cancellationToken: cancellationToken));
    }

    public async Task<ApiKey?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ApiKey>(
            new CommandDefinition(
                "SELECT * FROM ApiKeys WHERE Id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await _dialect.InsertAsync(conn,
            "INSERT INTO ApiKeys (UserId, KeyHash, Name, IsActive, CreatedByAdminId) VALUES (@UserId, @KeyHash, @Name, @IsActive, @CreatedByAdminId)",
            apiKey);
    }

    public Task<int> CreateAsync(ApiKey apiKey, IDbConnection conn, IDbTransaction txn)
        => _dialect.InsertAsync(conn,
            "INSERT INTO ApiKeys (UserId, KeyHash, Name, IsActive, CreatedByAdminId) VALUES (@UserId, @KeyHash, @Name, @IsActive, @CreatedByAdminId)",
            apiKey, txn);

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE ApiKeys SET IsActive = @IsActive WHERE Id = @Id",
                new { Id = id, IsActive = false },
                cancellationToken: cancellationToken));
    }

    public async Task DeactivateAllForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE ApiKeys SET IsActive = @IsActive WHERE UserId = @UserId AND IsActive = @CurrentIsActive",
                new { UserId = userId, IsActive = false, CurrentIsActive = true },
                cancellationToken: cancellationToken));
    }

    public Task DeactivateAllForUserAsync(int userId, IDbConnection conn, IDbTransaction txn)
        => conn.ExecuteAsync(
            "UPDATE ApiKeys SET IsActive = @IsActive WHERE UserId = @UserId AND IsActive = @CurrentIsActive",
            new { UserId = userId, IsActive = false, CurrentIsActive = true },
            transaction: txn);
}
