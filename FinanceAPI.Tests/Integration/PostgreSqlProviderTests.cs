using System.Data;
using Dapper;
using FinanceAPI.Database;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FinanceAPI.Tests.Integration;

public class PostgreSqlProviderTests : IClassFixture<FinanceApiFactory>, IAsyncLifetime
{
    private readonly FinanceApiFactory _factory;
    private int _seedUserId;

    public PostgreSqlProviderTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        _seedUserId = await TestDataSeeder.SeedAsync(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedUser_CanLoginViaApi()
    {
        if (!TestDataSeeder.IsPostgreSql)
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
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        int count = conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM Transactions WHERE UserId = @U
              AND Description IN ('Monatsmiete Januar','Gehalt Januar','Wocheneinkauf','Precision Test','Grossbetrag')",
            new { U = _seedUserId });

        Assert.Equal(5, count);
    }

    [Fact]
    public void SeedData_CategoriesExist()
    {
        if (!TestDataSeeder.IsPostgreSql)
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
    public void PostgreSql_NumericPrecision_PreservesExactDecimals()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        // NUMERIC(18,2) should preserve exact decimal values — no floating point drift
        List<decimal> amounts = conn.Query<decimal>(@"
            SELECT Amount FROM Transactions
            WHERE UserId = @U AND Description IN
                ('Monatsmiete Januar','Gehalt Januar','Wocheneinkauf','Precision Test','Grossbetrag')
            ORDER BY Date",
            new { U = _seedUserId }).ToList();

        Assert.Equal(5, amounts.Count);
        Assert.Equal(3500.50m, amounts[0]); // Gehalt Januar
        Assert.Equal(89.97m, amounts[1]); // Wocheneinkauf
        Assert.Equal(1250.99m, amounts[2]); // Monatsmiete
        Assert.Equal(0.01m, amounts[3]); // Precision Test
        Assert.Equal(99999.99m, amounts[4]); // Grossbetrag
    }

    [Fact]
    public void PostgreSql_CaseInsensitiveIndex_FindsUserByLower()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        // PostgreSQL uses LOWER() functional index for case-insensitive lookup
        int? found = conn.ExecuteScalar<int?>(
            "SELECT Id FROM Users WHERE LOWER(Username) = LOWER('SEEDUSER')");

        Assert.NotNull(found);
        Assert.Equal(_seedUserId, found);
    }

    [Fact]
    public void PostgreSql_BooleanType_NativeBoolean()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        // PostgreSQL stores IsActive as native BOOLEAN
        bool isActive = conn.ExecuteScalar<bool>(
            "SELECT IsActive FROM Users WHERE Id = @U",
            new { U = _seedUserId });

        Assert.True(isActive);
    }

    [Fact]
    public void PostgreSql_SumAggregation_ReturnsExactTotals()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        decimal income = conn.ExecuteScalar<decimal>(@"
            SELECT COALESCE(SUM(Amount), 0) FROM Transactions
            WHERE UserId = @U AND Type = 'income'
              AND Description IN ('Gehalt Januar','Grossbetrag')",
            new { U = _seedUserId });
        decimal expense = conn.ExecuteScalar<decimal>(@"
            SELECT COALESCE(SUM(Amount), 0) FROM Transactions
            WHERE UserId = @U AND Type = 'expense'
              AND Description IN ('Monatsmiete Januar','Wocheneinkauf','Precision Test')",
            new { U = _seedUserId });

        // NUMERIC sum is exact — no floating point drift
        Assert.Equal(103500.49m, income);
        Assert.Equal(1340.97m, expense);
    }

    [Fact]
    public void PostgreSql_SerialAutoIncrement_GeneratesIds()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        List<int> ids = conn.Query<int>(@"
            SELECT Id FROM Transactions
            WHERE UserId = @U AND Description IN
                ('Monatsmiete Januar','Gehalt Januar','Wocheneinkauf','Precision Test','Grossbetrag')
            ORDER BY Id",
            new { U = _seedUserId }).ToList();

        // SERIAL generates sequential, increasing IDs
        Assert.Equal(5, ids.Count);
        for (int i = 1; i < ids.Count; i++)
        {
            Assert.True(ids[i] > ids[i - 1]);
        }
    }

    // ──────────────────────────────────────────────
    //  Negative tests — constraint enforcement
    // ──────────────────────────────────────────────

    [Fact]
    public void PostgreSql_DuplicateUsername_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        // LOWER(Username) unique index prevents duplicates regardless of case
        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
                VALUES ('seeduser', 'other@test.com', @H, 'User', TRUE)",
                new { H = hash }));

        Assert.Equal("23505", ex.SqlState); // unique_violation
    }

    [Fact]
    public void PostgreSql_DuplicateUsernameCaseInsensitive_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        // "SEEDUSER" should also be blocked by LOWER() index
        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
                VALUES ('SEEDUSER', 'caseupper@test.com', @H, 'User', TRUE)",
                new { H = hash }));

        Assert.Equal("23505", ex.SqlState);
    }

    [Fact]
    public void PostgreSql_DuplicateEmail_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
                VALUES ('unique_pg_user', 'seed@test.com', @H, 'User', TRUE)",
                new { H = hash }));

        Assert.Equal("23505", ex.SqlState);
    }

    [Fact]
    public void PostgreSql_DuplicateCategoryName_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Categories (UserId, Name, Color, Type, SortOrder)
                VALUES (@U, 'Miete', '#000', 'expense', 99)",
                new { U = _seedUserId }));

        Assert.Equal("23505", ex.SqlState);
    }

    [Fact]
    public void PostgreSql_NegativeAmount_ThrowsCheckConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (@U, -50.00, 'expense', '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Equal("23514", ex.SqlState); // check_violation
    }

    [Fact]
    public void PostgreSql_ZeroAmount_ThrowsCheckConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (@U, 0, 'expense', '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public void PostgreSql_InvalidTransactionType_ThrowsCheckConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (@U, 10.00, 'refund', '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public void PostgreSql_ForeignKey_InvalidUserId_ThrowsConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (99999, 10.00, 'expense', '2026-01-01')"));

        Assert.Equal("23503", ex.SqlState); // foreign_key_violation
    }

    [Fact]
    public void PostgreSql_ForeignKey_InvalidCategoryId_ThrowsConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date)
                VALUES (@U, 10.00, 'expense', 99999, '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Equal("23503", ex.SqlState);
    }

    [Fact]
    public void PostgreSql_NotNull_MissingUsername_ThrowsConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash)
                VALUES (NULL, 'noname@test.com', @H)",
                new { H = hash }));

        Assert.Equal("23502", ex.SqlState); // not_null_violation
    }

    [Fact]
    public void PostgreSql_InvalidRole_ThrowsForeignKeyConstraint()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();
        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        PostgresException ex = Assert.Throws<Npgsql.PostgresException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
                VALUES ('fakeRole_user', 'fakerole@test.com', @H, 'SuperAdmin', TRUE)",
                new { H = hash }));

        Assert.Equal("23503", ex.SqlState); // foreign_key_violation on Roles
    }

    [Fact]
    public void PostgreSql_CascadeDelete_RemovesTransactionsWhenUserDeleted()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        string hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);
        int tempUserId = conn.ExecuteScalar<int>(@"
            INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
            VALUES ('pg_cascade', 'pgcascade@test.com', @H, 'User', TRUE)
            RETURNING Id",
            new { H = hash });

        conn.Execute(@"
            INSERT INTO Transactions (UserId, Amount, Type, Date)
            VALUES (@U, 100, 'income', '2026-01-01')",
            new { U = tempUserId });

        Assert.Equal(1, conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Transactions WHERE UserId = @U", new { U = tempUserId }));

        conn.Execute("DELETE FROM Users WHERE Id = @U", new { U = tempUserId });

        Assert.Equal(0, conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Transactions WHERE UserId = @U", new { U = tempUserId }));
    }

    [Fact]
    public void PostgreSql_SetNull_CategoryDeletionNullifiesTransactions()
    {
        if (!TestDataSeeder.IsPostgreSql)
        {
            return;
        }

        using IDbConnection conn = GetConnection();

        conn.Execute(@"
            INSERT INTO Categories (UserId, Name, Color, Type, SortOrder)
            VALUES (@U, 'PgTempCat', '#aaa', 'expense', 99)",
            new { U = _seedUserId });
        int catId = conn.ExecuteScalar<int>(
            "SELECT Id FROM Categories WHERE UserId = @U AND Name = 'PgTempCat'",
            new { U = _seedUserId });

        conn.Execute(@"
            INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date)
            VALUES (@U, 25, 'expense', @C, '2026-06-01')",
            new { U = _seedUserId, C = catId });
        int txId = conn.ExecuteScalar<int>(
            "SELECT Id FROM Transactions WHERE CategoryId = @C", new { C = catId });

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
