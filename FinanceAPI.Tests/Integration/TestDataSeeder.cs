using System.Collections.Concurrent;
using System.Data;
using Dapper;
using FinanceAPI.Database;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceAPI.Tests.Integration;

/// <summary>
/// Seeds provider-specific test data directly into the database.
/// Each provider gets its own seed method with appropriate SQL syntax and test values.
/// Seeding is idempotent per factory instance — multiple calls return the cached user ID.
/// </summary>
public static class TestDataSeeder
{
    private static readonly ConcurrentDictionary<int, int> _seededFactories = new();

    public static string Provider =>
        Environment.GetEnvironmentVariable("DatabaseSettings__Provider") ?? "sqlite";

    public static bool IsPostgreSql =>
        Provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase)
        || Provider.Equals("postgres", StringComparison.OrdinalIgnoreCase);

    public static bool IsMySql =>
        Provider.Equals("mysql", StringComparison.OrdinalIgnoreCase);

    public static bool IsSqlite => !IsPostgreSql && !IsMySql;

    public const string SeedUsername = "seeduser";
    public const string SeedPassword = "SeedPassword123!";

    /// <summary>
    /// Seeds test data appropriate for the active database provider.
    /// Returns the seeded user's ID. Safe to call multiple times per factory.
    /// </summary>
    public static async Task<int> SeedAsync(FinanceApiFactory factory)
    {
        var factoryKey = factory.GetHashCode();
        if (_seededFactories.TryGetValue(factoryKey, out var cachedUserId))
            return cachedUserId;

        // Trigger app startup (which runs DatabaseInitializer / schema creation)
        _ = factory.CreateClient();

        var dbFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
        using var conn = dbFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(SeedPassword, workFactor: 4);

        int userId;
        if (IsPostgreSql)
            userId = await SeedPostgreSqlAsync(conn, passwordHash);
        else if (IsMySql)
            userId = await SeedMySqlAsync(conn, passwordHash);
        else
            userId = await SeedSqliteAsync(conn, passwordHash);

        _seededFactories.TryAdd(factoryKey, userId);
        return userId;
    }

    private static async Task<int> SeedSqliteAsync(IDbConnection conn, string passwordHash)
    {
        await conn.ExecuteAsync(@"
            INSERT OR IGNORE INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
            VALUES ('seeduser', 'seed@test.com', @Hash, 'User', 1)",
            new { Hash = passwordHash });

        var userId = await conn.ExecuteScalarAsync<int>(
            "SELECT Id FROM Users WHERE Username = 'seeduser'");

        await conn.ExecuteAsync(@"
            INSERT OR IGNORE INTO Categories (UserId, Name, Color, Type, SortOrder) VALUES
            (@U, 'Miete',          '#e74c3c', 'expense', 1),
            (@U, 'Gehalt',         '#2ecc71', 'income',  2),
            (@U, 'Lebensmittel',   '#3498db', 'expense', 3)",
            new { U = userId });

        var cats = (await conn.QueryAsync<(int Id, string Name)>(
            "SELECT Id, Name FROM Categories WHERE UserId = @U ORDER BY SortOrder",
            new { U = userId })).ToDictionary(c => c.Name, c => c.Id);

        // SQLite uses REAL (IEEE 754 float) — test floating-point edge cases
        await conn.ExecuteAsync(@"
            INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date, Description) VALUES
            (@U, 1250.99,  'expense', @Miete,  '2026-01-15', 'Monatsmiete Januar'),
            (@U, 3500.50,  'income',  @Gehalt, '2026-01-01', 'Gehalt Januar'),
            (@U, 89.97,    'expense', @Food,   '2026-01-10', 'Wocheneinkauf'),
            (@U, 0.01,     'expense', NULL,    '2026-01-20', 'Kleinbetrag'),
            (@U, 9999.99,  'income',  NULL,    '2026-02-01', 'Bonus')",
            new
            {
                U = userId,
                Miete = cats["Miete"],
                Gehalt = cats["Gehalt"],
                Food = cats["Lebensmittel"]
            });

        return userId;
    }

    private static async Task<int> SeedPostgreSqlAsync(IDbConnection conn, string passwordHash)
    {
        // PostgreSQL: SERIAL + RETURNING, ON CONFLICT DO NOTHING
        var userId = await conn.ExecuteScalarAsync<int?>(@"
            INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
            VALUES ('seeduser', 'seed@test.com', @Hash, 'User', TRUE)
            ON CONFLICT DO NOTHING
            RETURNING Id",
            new { Hash = passwordHash });

        userId ??= await conn.ExecuteScalarAsync<int>(
            "SELECT Id FROM Users WHERE LOWER(Username) = 'seeduser'");

        await conn.ExecuteAsync(@"
            INSERT INTO Categories (UserId, Name, Color, Type, SortOrder) VALUES
            (@U, 'Miete',          '#e74c3c', 'expense', 1),
            (@U, 'Gehalt',         '#2ecc71', 'income',  2),
            (@U, 'Lebensmittel',   '#3498db', 'expense', 3)
            ON CONFLICT DO NOTHING",
            new { U = userId });

        var cats = (await conn.QueryAsync<(int Id, string Name)>(
            "SELECT Id, Name FROM Categories WHERE UserId = @U ORDER BY SortOrder",
            new { U = userId })).ToDictionary(c => c.Name, c => c.Id);

        // PostgreSQL uses NUMERIC(18,2) — exact decimal arithmetic
        await conn.ExecuteAsync(@"
            INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date, Description) VALUES
            (@U, 1250.99,   'expense', @Miete,  '2026-01-15', 'Monatsmiete Januar'),
            (@U, 3500.50,   'income',  @Gehalt, '2026-01-01', 'Gehalt Januar'),
            (@U, 89.97,     'expense', @Food,   '2026-01-10', 'Wocheneinkauf'),
            (@U, 0.01,      'expense', NULL,    '2026-01-20', 'Precision Test'),
            (@U, 99999.99,  'income',  NULL,    '2026-02-01', 'Grossbetrag')",
            new
            {
                U = userId,
                Miete = cats["Miete"],
                Gehalt = cats["Gehalt"],
                Food = cats["Lebensmittel"]
            });

        return (int)userId;
    }

    private static async Task<int> SeedMySqlAsync(IDbConnection conn, string passwordHash)
    {
        // MySQL: INSERT IGNORE, LAST_INSERT_ID()
        await conn.ExecuteAsync(@"
            INSERT IGNORE INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
            VALUES ('seeduser', 'seed@test.com', @Hash, 'User', 1)",
            new { Hash = passwordHash });

        var userId = await conn.ExecuteScalarAsync<int>(
            "SELECT Id FROM Users WHERE Username = 'seeduser'");

        await conn.ExecuteAsync(@"
            INSERT IGNORE INTO Categories (UserId, Name, Color, Type, SortOrder) VALUES
            (@U, 'Miete',          '#e74c3c', 'expense', 1),
            (@U, 'Gehalt',         '#2ecc71', 'income',  2),
            (@U, 'Lebensmittel',   '#3498db', 'expense', 3)",
            new { U = userId });

        var cats = (await conn.QueryAsync<(int Id, string Name)>(
            "SELECT Id, Name FROM Categories WHERE UserId = @U ORDER BY SortOrder",
            new { U = userId })).ToDictionary(c => c.Name, c => c.Id);

        // MySQL uses DECIMAL(18,2) — exact decimal arithmetic + ENUM validation
        await conn.ExecuteAsync(@"
            INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date, Description) VALUES
            (@U, 1250.99,   'expense', @Miete,  '2026-01-15', 'Monatsmiete Januar'),
            (@U, 3500.50,   'income',  @Gehalt, '2026-01-01', 'Gehalt Januar'),
            (@U, 89.97,     'expense', @Food,   '2026-01-10', 'Wocheneinkauf'),
            (@U, 0.01,      'expense', NULL,    '2026-01-20', 'Precision Test'),
            (@U, 99999.99,  'income',  NULL,    '2026-02-01', 'Grossbetrag')",
            new
            {
                U = userId,
                Miete = cats["Miete"],
                Gehalt = cats["Gehalt"],
                Food = cats["Lebensmittel"]
            });

        return userId;
    }
}
