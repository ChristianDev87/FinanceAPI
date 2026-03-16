using System.Data;
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
        string schemaFile = provider.ToLowerInvariant() switch
        {
            "postgresql" or "postgres" => "schema.postgresql.sql",
            "mysql" => "schema.mysql.sql",
            _ => "schema.sql"
        };

        string schemaPath = Path.Combine(AppContext.BaseDirectory, "Database", schemaFile);
        if (!File.Exists(schemaPath))
        {
            schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "Database", schemaFile);
        }

        // Fallback to schema.sql for backward compatibility
        if (!File.Exists(schemaPath))
        {
            schemaPath = Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql");
        }

        string schema = await File.ReadAllTextAsync(schemaPath);

        string normalizedProvider = provider.ToLowerInvariant();
        using IDbConnection connection = _connectionFactory.CreateConnection();

        // Acquire a provider-specific advisory lock so concurrent app instances
        // (e.g. multiple xUnit test factories against a shared database) don't race
        // to create the same tables/types simultaneously.
        // SQLite in-memory: each factory has an isolated database → no lock needed.
        switch (normalizedProvider)
        {
            case "postgresql" or "postgres":
                await connection.ExecuteAsync("SELECT pg_advisory_lock(987654321)");
                break;
            case "mysql":
                await connection.ExecuteAsync("SELECT GET_LOCK('financeapi_schema_init', 30)");
                break;
        }

        try
        {
            string[] statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string statement in statements)
            {
                if (string.IsNullOrWhiteSpace(statement))
                {
                    continue;
                }

                try
                {
                    await connection.ExecuteAsync(statement);
                }
                catch (Exception ex) when (IsAlreadyExistsError(ex, normalizedProvider, statement))
                {
                    // Object already exists — safe to skip on subsequent startups
                    _logger.LogDebug("Statement skipped (already exists): {Message}", ex.Message);
                }
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

    /// <summary>
    /// Returns true when the exception indicates that the database object already exists
    /// and the statement can be safely skipped.
    /// Each provider uses a different error code convention.
    /// </summary>
    private static bool IsAlreadyExistsError(Exception ex, string provider, string statement)
    {
        switch (provider)
        {
            case "postgresql" or "postgres":
                // 42P07 = duplicate_table, 42701 = duplicate_column, 42P01 = undefined_table (ADD COLUMN on missing table)
                // 23505 = unique_violation (concurrent INSERT of seed rows), 42710 = duplicate_object (index)
                if (ex is Npgsql.PostgresException pg)
                {
                    return pg.SqlState is "42P07" or "42701" or "23505" or "42710";
                }

                return false;

            case "mysql":
                // 1050 = table already exists, 1060 = duplicate column, 1061 = duplicate key name (index)
                // 1062 = duplicate entry (seed rows), 1005 = can't create table (FK already satisfied)
                if (ex is MySqlConnector.MySqlException my)
                {
                    return my.ErrorCode is MySqlConnector.MySqlErrorCode.TableExists
                                        or MySqlConnector.MySqlErrorCode.DuplicateFieldName
                                        or MySqlConnector.MySqlErrorCode.DuplicateKeyName
                                        or MySqlConnector.MySqlErrorCode.DuplicateKeyEntry;
                }

                return false;

            default: // SQLite — isolated in-memory databases per factory, only migration statements can fail
                return statement.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase)
                    || statement.Contains("CREATE INDEX", StringComparison.OrdinalIgnoreCase)
                    || statement.Contains("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase);
        }
    }
}
