using System.Data;
using Dapper;
using FinanceAPI.Database;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceAPI.Tests.Integration;

public class SqliteProviderTests : IClassFixture<FinanceApiFactory>, IAsyncLifetime
{
    private readonly FinanceApiFactory _factory;
    private int _seedUserId;

    public SqliteProviderTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        _seedUserId = await TestDataSeeder.SeedAsync(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedUser_CanLoginViaApi()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(
            _factory, TestDataSeeder.SeedUsername, TestDataSeeder.SeedPassword);

        HttpResponseMessage response = await client.GetAsync("/api/categories");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public void SeedData_TransactionsExist()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        int count = conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM Transactions WHERE UserId = @U
              AND Description IN ('Monatsmiete Januar','Gehalt Januar','Wocheneinkauf','Kleinbetrag','Bonus')",
            new { U = _seedUserId });

        Assert.Equal(5, count);
    }

    [Fact]
    public void SeedData_CategoriesExist()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        List<string> categories = conn.Query<string>(
            "SELECT Name FROM Categories WHERE UserId = @U ORDER BY SortOrder",
            new { U = _seedUserId }).ToList();

        Assert.Equal(3, categories.Count);
        Assert.Contains("Miete", categories);
        Assert.Contains("Gehalt", categories);
        Assert.Contains("Lebensmittel", categories);
    }

    [Fact]
    public void Sqlite_RealType_StoresDecimalAmounts()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        // SQLite REAL is IEEE 754 — verify amounts are retrievable and close to expected
        // Filter by known seed descriptions to avoid interference from other tests
        List<double> amounts = conn.Query<double>(@"
            SELECT Amount FROM Transactions
            WHERE UserId = @U AND Description IN
                ('Monatsmiete Januar','Gehalt Januar','Wocheneinkauf','Kleinbetrag','Bonus')
            ORDER BY Date",
            new { U = _seedUserId }).ToList();

        Assert.Equal(5, amounts.Count);
        Assert.Equal(3500.50, amounts[0], precision: 2); // Gehalt Januar (2026-01-01)
        Assert.Equal(89.97, amounts[1], precision: 2); // Wocheneinkauf (2026-01-10)
        Assert.Equal(1250.99, amounts[2], precision: 2); // Monatsmiete (2026-01-15)
        Assert.Equal(0.01, amounts[3], precision: 2); // Kleinbetrag (2026-01-20)
        Assert.Equal(9999.99, amounts[4], precision: 2); // Bonus (2026-02-01)
    }

    [Fact]
    public void Sqlite_TypeAffinity_StoresBooleansAsIntegers()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        // SQLite stores IsActive as INTEGER (0/1), not native BOOLEAN
        int isActive = conn.ExecuteScalar<int>(
            "SELECT IsActive FROM Users WHERE Id = @U",
            new { U = _seedUserId });

        Assert.Equal(1, isActive);
    }

    [Fact]
    public void Sqlite_CollateNocase_CaseInsensitiveLookup()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        // SQLite COLLATE NOCASE allows case-insensitive matching
        int? found = conn.ExecuteScalar<int?>(
            "SELECT Id FROM Users WHERE Username = 'SEEDUSER'");

        Assert.NotNull(found);
        Assert.Equal(_seedUserId, found);
    }

    [Fact]
    public void Sqlite_SumAggregation_ReturnsCorrectTotals()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        // Filter by seed descriptions to avoid interference from other tests
        double income = conn.ExecuteScalar<double>(@"
            SELECT COALESCE(SUM(Amount), 0) FROM Transactions
            WHERE UserId = @U AND Type = 'income'
              AND Description IN ('Gehalt Januar','Bonus')",
            new { U = _seedUserId });
        double expense = conn.ExecuteScalar<double>(@"
            SELECT COALESCE(SUM(Amount), 0) FROM Transactions
            WHERE UserId = @U AND Type = 'expense'
              AND Description IN ('Monatsmiete Januar','Wocheneinkauf','Kleinbetrag')",
            new { U = _seedUserId });

        Assert.Equal(13500.49, income, precision: 2);
        Assert.Equal(1340.97, expense, precision: 2);
    }

    // ──────────────────────────────────────────────
    //  Negative tests — constraint enforcement
    // ──────────────────────────────────────────────

    [Fact]
    public void Sqlite_DuplicateUsername_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        Microsoft.Data.Sqlite.SqliteException ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
                VALUES ('seeduser', 'other@test.com', @H, 'User', 1)",
                new { H = hash }));

        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_DuplicateEmail_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        Microsoft.Data.Sqlite.SqliteException ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
                VALUES ('unique_user', 'seed@test.com', @H, 'User', 1)",
                new { H = hash }));

        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_DuplicateCategoryName_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        // UNIQUE(UserId, Name) — same user, same category name
        Microsoft.Data.Sqlite.SqliteException ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(@"
                INSERT INTO Categories (UserId, Name, Color, Type, SortOrder)
                VALUES (@U, 'Miete', '#000', 'expense', 99)",
                new { U = _seedUserId }));

        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_NegativeAmount_ThrowsCheckConstraint()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        Microsoft.Data.Sqlite.SqliteException ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (@U, -50.00, 'expense', '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_ZeroAmount_ThrowsCheckConstraint()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        Microsoft.Data.Sqlite.SqliteException ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (@U, 0, 'expense', '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_InvalidTransactionType_ThrowsCheckConstraint()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        Microsoft.Data.Sqlite.SqliteException ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (@U, 10.00, 'refund', '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_ForeignKey_InvalidUserId_ThrowsConstraint()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        // Enable FK enforcement (SQLite requires explicit PRAGMA)
        conn.Execute("PRAGMA foreign_keys = ON");

        Microsoft.Data.Sqlite.SqliteException ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (99999, 10.00, 'expense', '2026-01-01')"));

        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_ForeignKey_InvalidCategoryId_ThrowsConstraint()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        conn.Execute("PRAGMA foreign_keys = ON");

        Microsoft.Data.Sqlite.SqliteException ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date)
                VALUES (@U, 10.00, 'expense', 99999, '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_NotNull_MissingUsername_ThrowsConstraint()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        Microsoft.Data.Sqlite.SqliteException ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash)
                VALUES (NULL, 'noname@test.com', @H)",
                new { H = hash }));

        Assert.Contains("NOT NULL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_CascadeDelete_RemovesTransactionsWhenUserDeleted()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        conn.Execute("PRAGMA foreign_keys = ON");

        // Create a temporary user with a transaction
        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);
        conn.Execute(@"
            INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
            VALUES ('cascade_test', 'cascade@test.com', @H, 'User', 1)",
            new { H = hash });
        int tempUserId = conn.ExecuteScalar<int>("SELECT Id FROM Users WHERE Username = 'cascade_test'");

        conn.Execute(@"
            INSERT INTO Transactions (UserId, Amount, Type, Date)
            VALUES (@U, 100, 'income', '2026-01-01')",
            new { U = tempUserId });

        int countBefore = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Transactions WHERE UserId = @U", new { U = tempUserId });
        Assert.Equal(1, countBefore);

        // Delete user — CASCADE should remove transactions
        conn.Execute("DELETE FROM Users WHERE Id = @U", new { U = tempUserId });

        int countAfter = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Transactions WHERE UserId = @U", new { U = tempUserId });
        Assert.Equal(0, countAfter);
    }

    [Fact]
    public void Sqlite_SetNull_CategoryDeletionNullifiesTransactions()
    {
        if (!TestDataSeeder.IsSqlite)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        conn.Execute("PRAGMA foreign_keys = ON");

        // Create a temporary category + transaction
        conn.Execute(@"
            INSERT INTO Categories (UserId, Name, Color, Type, SortOrder)
            VALUES (@U, 'TempCat', '#aaa', 'expense', 99)",
            new { U = _seedUserId });
        int catId = conn.ExecuteScalar<int>(
            "SELECT Id FROM Categories WHERE UserId = @U AND Name = 'TempCat'",
            new { U = _seedUserId });

        conn.Execute(@"
            INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date)
            VALUES (@U, 25, 'expense', @C, '2026-06-01')",
            new { U = _seedUserId, C = catId });
        int txId = conn.ExecuteScalar<int>(
            "SELECT Id FROM Transactions WHERE UserId = @U AND CategoryId = @C",
            new { U = _seedUserId, C = catId });

        // Delete category — ON DELETE SET NULL should nullify CategoryId
        conn.Execute("DELETE FROM Categories WHERE Id = @C", new { C = catId });

        int? categoryIdAfter = conn.ExecuteScalar<int?>(
            "SELECT CategoryId FROM Transactions WHERE Id = @Id", new { Id = txId });
        Assert.Null(categoryIdAfter);
    }

    private IDbConnection GetConnection()
    {
        IDbConnectionFactory dbFactory = _factory.Services.GetRequiredService<IDbConnectionFactory>();
        IDbConnection conn = dbFactory.CreateConnection();
        if (conn.State != ConnectionState.Open)
        {
            conn.Open();
        }

        return conn;
    }
}
