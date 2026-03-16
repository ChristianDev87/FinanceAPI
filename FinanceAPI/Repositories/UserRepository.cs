using System.Data;
using Dapper;
using FinanceAPI.Database;
using FinanceAPI.Domain;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;

namespace FinanceAPI.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISqlDialect _dialect;

    public UserRepository(IDbConnectionFactory connectionFactory, ISqlDialect dialect)
    {
        _connectionFactory = connectionFactory;
        _dialect = dialect;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                "SELECT * FROM Users WHERE Id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                $"SELECT * FROM Users WHERE {_dialect.CaseInsensitiveEqual("Username", "@Username")}",
                new { Username = username },
                cancellationToken: cancellationToken));
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                $"SELECT * FROM Users WHERE {_dialect.CaseInsensitiveEqual("Email", "@Email")}",
                new { Email = email },
                cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<User>(
            new CommandDefinition(
                "SELECT * FROM Users ORDER BY CreatedAt DESC",
                cancellationToken: cancellationToken));
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM Users",
                cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await _dialect.InsertAsync(conn,
            "INSERT INTO Users (Username, Email, PasswordHash, RoleName) VALUES (@Username, @Email, @PasswordHash, @RoleName)",
            user);
    }

    public Task<int> CreateAsync(User user, IDbConnection conn, IDbTransaction txn)
        => _dialect.InsertAsync(conn,
            "INSERT INTO Users (Username, Email, PasswordHash, RoleName) VALUES (@Username, @Email, @PasswordHash, @RoleName)",
            user, txn);

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET Username = @Username, Email = @Email, RoleName = @RoleName WHERE Id = @Id",
                user,
                cancellationToken: cancellationToken));
    }

    public async Task UpdatePasswordAsync(int id, string passwordHash, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET PasswordHash = @PasswordHash WHERE Id = @Id",
                new { Id = id, PasswordHash = passwordHash },
                cancellationToken: cancellationToken));
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET IsActive = @IsActive WHERE Id = @Id",
                new { Id = id, IsActive = isActive },
                cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Users WHERE Id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM Users WHERE RoleName = @RoleName AND IsActive = @IsActive",
                new { RoleName = UserRoles.Admin, IsActive = true },
                cancellationToken: cancellationToken));
    }

    // ── Transactional overloads ──────────────────────────────────

    public Task<User?> GetByUsernameAsync(string username, IDbConnection conn, IDbTransaction txn)
        => conn.QuerySingleOrDefaultAsync<User>(
            $"SELECT * FROM Users WHERE {_dialect.CaseInsensitiveEqual("Username", "@Username")}",
            new { Username = username },
            transaction: txn);

    public Task<User?> GetByEmailAsync(string email, IDbConnection conn, IDbTransaction txn)
        => conn.QuerySingleOrDefaultAsync<User>(
            $"SELECT * FROM Users WHERE {_dialect.CaseInsensitiveEqual("Email", "@Email")}",
            new { Email = email },
            transaction: txn);

    public async Task<bool> AnyAsync(IDbConnection conn, IDbTransaction txn)
    {
        int count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Users",
            transaction: txn);
        return count > 0;
    }

    public Task<int> CountActiveAdminsAsync(IDbConnection conn, IDbTransaction txn)
        => conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Users WHERE RoleName = @RoleName AND IsActive = @IsActive",
            new { RoleName = UserRoles.Admin, IsActive = true },
            transaction: txn);

    public Task UpdateAsync(User user, IDbConnection conn, IDbTransaction txn)
        => conn.ExecuteAsync(
            "UPDATE Users SET Username = @Username, Email = @Email, RoleName = @RoleName WHERE Id = @Id",
            user,
            transaction: txn);

    public Task DeleteAsync(int id, IDbConnection conn, IDbTransaction txn)
        => conn.ExecuteAsync(
            "DELETE FROM Users WHERE Id = @Id",
            new { Id = id },
            transaction: txn);

    public Task SetActiveAsync(int id, bool isActive, IDbConnection conn, IDbTransaction txn)
        => conn.ExecuteAsync(
            "UPDATE Users SET IsActive = @IsActive WHERE Id = @Id",
            new { Id = id, IsActive = isActive },
            transaction: txn);
}
