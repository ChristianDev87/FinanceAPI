using System.Data;
using Dapper;
using FinanceAPI.Database;
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

    public async Task<User?> GetByIdAsync(int id)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id", new { Id = id });
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"SELECT * FROM Users WHERE {_dialect.CaseInsensitiveEqual("Username", "@Username")}",
            new { Username = username });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            $"SELECT * FROM Users WHERE {_dialect.CaseInsensitiveEqual("Email", "@Email")}",
            new { Email = email });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<User>("SELECT * FROM Users ORDER BY CreatedAt DESC");
    }

    public async Task<bool> AnyAsync()
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users") > 0;
    }

    public async Task<int> CreateAsync(User user)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await _dialect.InsertAsync(conn,
            "INSERT INTO Users (Username, Email, PasswordHash, RoleName) VALUES (@Username, @Email, @PasswordHash, @RoleName)",
            user);
    }

    public async Task UpdateAsync(User user)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET Username = @Username, Email = @Email, RoleName = @RoleName WHERE Id = @Id",
            user);
    }

    public async Task UpdatePasswordAsync(int id, string passwordHash)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET PasswordHash = @PasswordHash WHERE Id = @Id",
            new { Id = id, PasswordHash = passwordHash });
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET IsActive = @IsActive WHERE Id = @Id",
            new { Id = id, IsActive = isActive });
    }

    public async Task DeleteAsync(int id)
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Users WHERE Id = @Id", new { Id = id });
    }

    public async Task<int> CountActiveAdminsAsync()
    {
        using IDbConnection conn = _connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Users WHERE RoleName = 'Admin' AND IsActive = @IsActive",
            new { IsActive = true });
    }
}
