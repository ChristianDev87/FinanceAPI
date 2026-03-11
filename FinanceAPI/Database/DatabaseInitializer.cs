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

        var provider = _configuration["DatabaseSettings:Provider"] ?? "sqlite";
        var schemaFile = provider.ToLowerInvariant() switch
        {
            "postgresql" or "postgres" => "schema.postgresql.sql",
            "mysql"                    => "schema.mysql.sql",
            _                          => "schema.sql"
        };

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Database", schemaFile);
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "Database", schemaFile);

        // Fallback to schema.sql for backward compatibility
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql");

        var schema = await File.ReadAllTextAsync(schemaPath);

        using var connection = _connectionFactory.CreateConnection();

        var statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;
            try
            {
                await connection.ExecuteAsync(statement);
            }
            catch (Exception ex) when (
                statement.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase) ||
                statement.Contains("CREATE INDEX", StringComparison.OrdinalIgnoreCase) ||
                statement.Contains("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase))
            {
                // Column or index likely already exists — safe to ignore on subsequent startups
                _logger.LogDebug("Statement skipped (may already exist): {Message}", ex.Message);
            }
        }

        _logger.LogInformation("Database initialized.");
    }
}
