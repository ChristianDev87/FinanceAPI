using Dapper;
using FinanceAPI.Database;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;

namespace FinanceAPI.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id", new { Id = id });
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Username = @Username COLLATE NOCASE", new { Username = username });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Email = @Email COLLATE NOCASE", new { Email = email });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QueryAsync<User>("SELECT * FROM Users ORDER BY CreatedAt DESC");
    }

    public async Task<int> CreateAsync(User user)
    {
        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleAsync<int>(
            """
            INSERT INTO Users (Username, Email, PasswordHash, RoleName)
            VALUES (@Username, @Email, @PasswordHash, @RoleName);
            SELECT last_insert_rowid();
            """, user);
    }

    public async Task UpdateAsync(User user)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET Username = @Username, Email = @Email, RoleName = @RoleName WHERE Id = @Id",
            user);
    }

    public async Task UpdatePasswordAsync(int id, string passwordHash)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET PasswordHash = @PasswordHash WHERE Id = @Id",
            new { Id = id, PasswordHash = passwordHash });
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET IsActive = @IsActive WHERE Id = @Id",
            new { Id = id, IsActive = isActive ? 1 : 0 });
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Users WHERE Id = @Id", new { Id = id });
    }
}
