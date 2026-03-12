using System.Data;
using Dapper;
using FinanceAPI.Database;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceAPI.Tests.Integration;

public class MySqlProviderTests : IClassFixture<FinanceApiFactory>, IAsyncLifetime
{
    private readonly FinanceApiFactory _factory;
    private int _seedUserId;

    public MySqlProviderTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        if (!TestDataSeeder.IsMySql) return;
        _seedUserId = await TestDataSeeder.SeedAsync(_factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SeedUser_CanLoginViaApi()
    {
        if (!TestDataSeeder.IsMySql) return;

        var client = await TestHelpers.CreateAuthenticatedClientAsync(
            _factory, TestDataSeeder.SeedUsername, TestDataSeeder.SeedPassword);

        var response = await client.GetAsync("/api/categories");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public void SeedData_TransactionsExist()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();
        var count = conn.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM Transactions WHERE UserId = @U
              AND Description IN ('Monatsmiete Januar','Gehalt Januar','Wocheneinkauf','Precision Test','Grossbetrag')",
            new { U = _seedUserId });

        Assert.Equal(5, count);
    }

    [Fact]
    public void SeedData_CategoriesExist()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();
        var categories = conn.Query<string>(
            "SELECT Name FROM Categories WHERE UserId = @U ORDER BY SortOrder",
            new { U = _seedUserId }).ToList();

        Assert.Equal(3, categories.Count);
        Assert.Contains("Miete", categories);
        Assert.Contains("Gehalt", categories);
        Assert.Contains("Lebensmittel", categories);
    }

    [Fact]
    public void MySql_DecimalPrecision_PreservesExactDecimals()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        // DECIMAL(18,2) preserves exact decimal values
        var amounts = conn.Query<decimal>(@"
            SELECT Amount FROM Transactions
            WHERE UserId = @U AND Description IN
                ('Monatsmiete Januar','Gehalt Januar','Wocheneinkauf','Precision Test','Grossbetrag')
            ORDER BY Date",
            new { U = _seedUserId }).ToList();

        Assert.Equal(5, amounts.Count);
        Assert.Equal(3500.50m,  amounts[0]); // Gehalt Januar
        Assert.Equal(89.97m,    amounts[1]); // Wocheneinkauf
        Assert.Equal(1250.99m,  amounts[2]); // Monatsmiete
        Assert.Equal(0.01m,     amounts[3]); // Precision Test
        Assert.Equal(99999.99m, amounts[4]); // Grossbetrag
    }

    [Fact]
    public void MySql_CaseInsensitive_DefaultCollation()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        // MySQL default collation (utf8mb4_0900_ai_ci) is case-insensitive
        var found = conn.ExecuteScalar<int?>(
            "SELECT Id FROM Users WHERE Username = 'SEEDUSER'");

        Assert.NotNull(found);
        Assert.Equal(_seedUserId, found);
    }

    [Fact]
    public void MySql_EnumType_RejectsInvalidValues()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        // MySQL ENUM('income','expense') enforces valid values at the DB level
        var types = conn.Query<string>(
            "SELECT DISTINCT Type FROM Transactions WHERE UserId = @U",
            new { U = _seedUserId }).ToList();

        Assert.All(types, t => Assert.Contains(t, new[] { "income", "expense" }));
    }

    [Fact]
    public void MySql_TinyIntBoolean_StoresAsInteger()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        // MySQL uses TINYINT(1) for boolean — returns as integer
        var isActive = conn.ExecuteScalar<int>(
            "SELECT IsActive FROM Users WHERE Id = @U",
            new { U = _seedUserId });

        Assert.Equal(1, isActive);
    }

    [Fact]
    public void MySql_SumAggregation_ReturnsExactTotals()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        var income = conn.ExecuteScalar<decimal>(@"
            SELECT COALESCE(SUM(Amount), 0) FROM Transactions
            WHERE UserId = @U AND Type = 'income'
              AND Description IN ('Gehalt Januar','Grossbetrag')",
            new { U = _seedUserId });
        var expense = conn.ExecuteScalar<decimal>(@"
            SELECT COALESCE(SUM(Amount), 0) FROM Transactions
            WHERE UserId = @U AND Type = 'expense'
              AND Description IN ('Monatsmiete Januar','Wocheneinkauf','Precision Test')",
            new { U = _seedUserId });

        // DECIMAL sum is exact
        Assert.Equal(103500.49m, income);
        Assert.Equal(1340.97m,   expense);
    }

    [Fact]
    public void MySql_AutoIncrement_GeneratesSequentialIds()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        var ids = conn.Query<int>(@"
            SELECT Id FROM Transactions
            WHERE UserId = @U AND Description IN
                ('Monatsmiete Januar','Gehalt Januar','Wocheneinkauf','Precision Test','Grossbetrag')
            ORDER BY Id",
            new { U = _seedUserId }).ToList();

        Assert.Equal(5, ids.Count);
        for (int i = 1; i < ids.Count; i++)
            Assert.True(ids[i] > ids[i - 1]);
    }

    // ──────────────────────────────────────────────
    //  Negative tests — constraint enforcement
    // ──────────────────────────────────────────────

    [Fact]
    public void MySql_DuplicateUsername_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();
        var hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
                VALUES ('seeduser', 'other@test.com', @H, 'User', 1)",
                new { H = hash }));

        // MySQL error 1062 = Duplicate entry
        Assert.Equal(MySqlConnector.MySqlErrorCode.DuplicateKeyEntry, ex.ErrorCode);
    }

    [Fact]
    public void MySql_DuplicateEmail_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();
        var hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
                VALUES ('unique_my_user', 'seed@test.com', @H, 'User', 1)",
                new { H = hash }));

        Assert.Equal(MySqlConnector.MySqlErrorCode.DuplicateKeyEntry, ex.ErrorCode);
    }

    [Fact]
    public void MySql_DuplicateCategoryName_ThrowsUniqueConstraint()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Categories (UserId, Name, Color, Type, SortOrder)
                VALUES (@U, 'Miete', '#000', 'expense', 99)",
                new { U = _seedUserId }));

        Assert.Equal(MySqlConnector.MySqlErrorCode.DuplicateKeyEntry, ex.ErrorCode);
    }

    [Fact]
    public void MySql_NegativeAmount_ThrowsCheckConstraint()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (@U, -50.00, 'expense', '2026-01-01')",
                new { U = _seedUserId }));

        // MySQL 8.0.16+ error 3819 = CHECK constraint violated
        Assert.Equal(3819, (int)ex.ErrorCode);
    }

    [Fact]
    public void MySql_ZeroAmount_ThrowsCheckConstraint()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (@U, 0, 'expense', '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Equal(3819, (int)ex.ErrorCode);
    }

    [Fact]
    public void MySql_InvalidEnumType_ThrowsDataTruncation()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        // MySQL ENUM rejects values not in the defined list
        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (@U, 10.00, 'refund', '2026-01-01')",
                new { U = _seedUserId }));

        // MySQL error 1265 = Data truncated for invalid ENUM value
        Assert.Equal(1265, (int)ex.ErrorCode);
    }

    [Fact]
    public void MySql_ForeignKey_InvalidUserId_ThrowsConstraint()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, Date)
                VALUES (99999, 10.00, 'expense', '2026-01-01')"));

        // MySQL error 1452 = Cannot add or update a child row: FK constraint fails
        Assert.Equal(MySqlConnector.MySqlErrorCode.NoReferencedRow2, ex.ErrorCode);
    }

    [Fact]
    public void MySql_ForeignKey_InvalidCategoryId_ThrowsConstraint()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date)
                VALUES (@U, 10.00, 'expense', 99999, '2026-01-01')",
                new { U = _seedUserId }));

        Assert.Equal(MySqlConnector.MySqlErrorCode.NoReferencedRow2, ex.ErrorCode);
    }

    [Fact]
    public void MySql_NotNull_MissingUsername_ThrowsConstraint()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();
        var hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash)
                VALUES (NULL, 'noname@test.com', @H)",
                new { H = hash }));

        // MySQL error 1048 = Column cannot be null
        Assert.Equal(MySqlConnector.MySqlErrorCode.ColumnCannotBeNull, ex.ErrorCode);
    }

    [Fact]
    public void MySql_InvalidRole_ThrowsForeignKeyConstraint()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();
        var hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);

        var ex = Assert.Throws<MySqlConnector.MySqlException>(() =>
            conn.Execute(@"
                INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
                VALUES ('fakeRole_user', 'fakerole@test.com', @H, 'SuperAdmin', 1)",
                new { H = hash }));

        Assert.Equal(MySqlConnector.MySqlErrorCode.NoReferencedRow2, ex.ErrorCode);
    }

    [Fact]
    public void MySql_CascadeDelete_RemovesTransactionsWhenUserDeleted()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        var hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);
        conn.Execute(@"
            INSERT INTO Users (Username, Email, PasswordHash, RoleName, IsActive)
            VALUES ('my_cascade', 'mycascade@test.com', @H, 'User', 1)",
            new { H = hash });
        var tempUserId = conn.ExecuteScalar<int>(
            "SELECT Id FROM Users WHERE Username = 'my_cascade'");

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
    public void MySql_SetNull_CategoryDeletionNullifiesTransactions()
    {
        if (!TestDataSeeder.IsMySql) return;

        using var conn = GetConnection();

        conn.Execute(@"
            INSERT INTO Categories (UserId, Name, Color, Type, SortOrder)
            VALUES (@U, 'MyTempCat', '#aaa', 'expense', 99)",
            new { U = _seedUserId });
        var catId = conn.ExecuteScalar<int>(
            "SELECT Id FROM Categories WHERE UserId = @U AND Name = 'MyTempCat'",
            new { U = _seedUserId });

        conn.Execute(@"
            INSERT INTO Transactions (UserId, Amount, Type, CategoryId, Date)
            VALUES (@U, 25, 'expense', @C, '2026-06-01')",
            new { U = _seedUserId, C = catId });
        var txId = conn.ExecuteScalar<int>(
            "SELECT Id FROM Transactions WHERE CategoryId = @C", new { C = catId });

        conn.Execute("DELETE FROM Categories WHERE Id = @C", new { C = catId });

        var categoryIdAfter = conn.ExecuteScalar<int?>(
            "SELECT CategoryId FROM Transactions WHERE Id = @Id", new { Id = txId });
        Assert.Null(categoryIdAfter);
    }

    private IDbConnection GetConnection()
    {
        var dbFactory = _factory.Services.GetRequiredService<IDbConnectionFactory>();
        var conn = dbFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        return conn;
    }
}
