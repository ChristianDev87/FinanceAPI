using Dapper;

namespace FinanceAPI.Database;

public class DatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing database...");

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql");
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "Database", "schema.sql");

        var schema = await File.ReadAllTextAsync(schemaPath);

        using var connection = _connectionFactory.CreateConnection();

        // Execute each statement separately (SQLite doesn't support multi-statement in one call)
        var statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;
            try
            {
                await connection.ExecuteAsync(statement);
            }
            catch (Exception ex) when (statement.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase))
            {
                // Column likely already exists — safe to ignore on subsequent startups
                _logger.LogDebug("ADD COLUMN skipped (column may already exist): {Message}", ex.Message);
            }
        }

        _logger.LogInformation("Database initialized.");
    }
}
