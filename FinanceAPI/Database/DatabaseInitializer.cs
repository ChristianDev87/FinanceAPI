using System.Data;
using System.Globalization;
using Dapper;

namespace FinanceAPI.Database;

public class DatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDbConnectionFactory connectionFactory,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing database...");

        string provider = _configuration["DatabaseSettings:Provider"] ?? "sqlite";
        string normalizedProvider = provider.ToLowerInvariant();

        using IDbConnection connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        // Acquire a provider-specific advisory lock so concurrent app instances
        // (e.g. multiple xUnit test factories against a shared database) don't race
        // to apply the same migrations simultaneously.
        switch (normalizedProvider)
        {
            case "postgresql" or "postgres":
                await connection.ExecuteAsync("SELECT pg_advisory_lock(987654321)");
                break;
            case "mysql":
                int? lockResult = await connection.ExecuteScalarAsync<int?>(
                    "SELECT GET_LOCK('financeapi_schema_init', 30)");
                if (lockResult != 1)
                {
                    throw new InvalidOperationException(
                        "Failed to acquire MySQL advisory lock for schema migration. " +
                        "Another instance may be holding the lock or an error occurred.");
                }
                break;
        }

        try
        {
            // 1. Ensure the SchemaVersions tracking table exists
            await EnsureSchemaVersionsTableAsync(connection, normalizedProvider);

            // 2. Load already-applied migration versions
            HashSet<int> applied = (await connection.QueryAsync<int>(
                "SELECT Version FROM SchemaVersions")).ToHashSet();

            // 3. Discover migration files for this provider, sorted by version number
            string migrationsDir = GetMigrationsDirectory(normalizedProvider);
            string[] migrationFiles = Directory.GetFiles(migrationsDir, "V*.sql")
                .OrderBy(f => f)
                .ToArray();

            // 4. Apply any pending migrations
            foreach (string file in migrationFiles)
            {
                int version = ParseVersion(Path.GetFileName(file));
                if (applied.Contains(version))
                {
                    continue;
                }

                _logger.LogInformation("Applying migration V{Version:D3} ({File})...",
                    version, Path.GetFileName(file));
                await RunMigrationAsync(connection, file, version, normalizedProvider);
            }
        }
        finally
        {
            switch (normalizedProvider)
            {
                case "postgresql" or "postgres":
                    await connection.ExecuteAsync("SELECT pg_advisory_unlock(987654321)");
                    break;
                case "mysql":
                    await connection.ExecuteAsync("SELECT RELEASE_LOCK('financeapi_schema_init')");
                    break;
            }
        }

        _logger.LogInformation("Database initialized.");
    }

    private static async Task EnsureSchemaVersionsTableAsync(IDbConnection connection, string provider)
    {
        string sql = provider switch
        {
            "mysql" =>
                "CREATE TABLE IF NOT EXISTS SchemaVersions (Version INT PRIMARY KEY, AppliedAt VARCHAR(50) NOT NULL)",
            _ => // sqlite + postgresql
                "CREATE TABLE IF NOT EXISTS SchemaVersions (Version INTEGER PRIMARY KEY, AppliedAt TEXT NOT NULL)"
        };

        await connection.ExecuteAsync(sql);
    }

    private string GetMigrationsDirectory(string provider)
    {
        string providerFolder = provider switch
        {
            "postgresql" or "postgres" => "PostgreSQL",
            "mysql" => "MySQL",
            _ => "SQLite"
        };

        string baseDir = Path.Combine(AppContext.BaseDirectory, "Database", "Migrations", providerFolder);
        if (Directory.Exists(baseDir))
        {
            return baseDir;
        }

        string fallback = Path.Combine(Directory.GetCurrentDirectory(), "Database", "Migrations", providerFolder);
        if (Directory.Exists(fallback))
        {
            return fallback;
        }

        throw new DirectoryNotFoundException(
            $"Migration directory not found for provider '{provider}'. Searched: '{baseDir}', '{fallback}'.");
    }

    private static int ParseVersion(string filename)
    {
        // Expected format: V001__description.sql → 1
        ReadOnlySpan<char> span = filename.AsSpan(1); // skip leading 'V'
        int underscoreIdx = span.IndexOf('_');
        ReadOnlySpan<char> versionSpan = underscoreIdx > 0 ? span[..underscoreIdx] : span;
        return int.Parse(versionSpan, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private async Task RunMigrationAsync(IDbConnection connection, string filePath, int version, string provider)
    {
        string schema = await File.ReadAllTextAsync(filePath);
        string[] statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                continue;
            }

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await connection.ExecuteAsync(statement);
                    break;
                }
                catch (Exception ex) when (IsAlreadyExistsError(ex, provider, statement))
                {
                    _logger.LogDebug("Statement skipped (already exists): {Message}", ex.Message);
                    break;
                }
                catch (Exception ex) when (IsDeadlockError(ex, provider))
                {
                    if (attempt >= maxAttempts)
                    {
                        _logger.LogError(ex, "Schema migration failed after {Max} deadlock retries.", maxAttempts);
                        throw;
                    }

                    _logger.LogWarning("Deadlock on migration attempt {Attempt}/{Max}, retrying in {Delay}ms...",
                        attempt, maxAttempts, 200 * attempt);
                    await Task.Delay(200 * attempt);
                }
            }
        }

        await connection.ExecuteAsync(
            "INSERT INTO SchemaVersions (Version, AppliedAt) VALUES (@Version, @AppliedAt)",
            new { Version = version, AppliedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) });

        _logger.LogInformation("Migration V{Version:D3} applied successfully.", version);
    }

    private static bool IsDeadlockError(Exception ex, string provider) =>
        provider is "mysql" &&
        ex is MySqlConnector.MySqlException { ErrorCode: MySqlConnector.MySqlErrorCode.LockDeadlock };

    private static bool IsAlreadyExistsError(Exception ex, string provider, string statement)
    {
        switch (provider)
        {
            case "postgresql" or "postgres":
                // 42P07 = duplicate_table, 42701 = duplicate_column, 42710 = duplicate_object (index)
                // 23505 = unique_violation (seed rows)
                if (ex is Npgsql.PostgresException pg)
                {
                    return pg.SqlState is "42P07" or "42701" or "23505" or "42710";
                }

                return false;

            case "mysql":
                // 1050 = table already exists, 1060 = duplicate column, 1061 = duplicate key name
                // 1062 = duplicate entry (seed rows)
                if (ex is MySqlConnector.MySqlException my)
                {
                    return my.ErrorCode is MySqlConnector.MySqlErrorCode.TableExists
                                        or MySqlConnector.MySqlErrorCode.DuplicateFieldName
                                        or MySqlConnector.MySqlErrorCode.DuplicateKeyName
                                        or MySqlConnector.MySqlErrorCode.DuplicateKeyEntry;
                }

                return false;

            default: // SQLite — only suppress errors that are provably idempotent
                if (ex is not Microsoft.Data.Sqlite.SqliteException)
                {
                    return false;
                }

                string msg = ex.Message;
                if (statement.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase)
                    && msg.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if ((statement.Contains("CREATE INDEX", StringComparison.OrdinalIgnoreCase)
                     || statement.Contains("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase))
                    && msg.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
        }
    }
}
