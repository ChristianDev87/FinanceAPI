using FinanceAPI.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceAPI.Tests.Integration;

/// <summary>
/// A WebApplicationFactory that runs the FinanceAPI against an isolated database.
/// By default it uses a named SQLite in-memory database, but when the environment
/// variables DatabaseSettings__Provider and ConnectionStrings__DefaultConnection
/// are set (as in CI), it uses the real database provider instead.
/// </summary>
public sealed class FinanceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _provider;
    private readonly string _connectionString;
    private readonly string _dbName = $"finance_test_{Guid.NewGuid():N}";
    private SqliteConnection? _keepAlive;

    public FinanceApiFactory()
    {
        _provider = Environment.GetEnvironmentVariable("DatabaseSettings__Provider") ?? "sqlite";
        _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                            ?? $"Data Source={_dbName};Mode=Memory;Cache=Shared";
    }

    private bool IsSqliteInMemory =>
        _provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase)
        && _connectionString.Contains("Memory", StringComparison.OrdinalIgnoreCase);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", _connectionString },
                { "DatabaseSettings:Provider",           _provider },
                { "JwtSettings:SecretKey",              "super-secret-key-for-integration-tests-32chars!!" },
                { "JwtSettings:Issuer",                 "FinanceAPI-Test" },
                { "JwtSettings:Audience",               "FinanceAPI-Test" },
                { "JwtSettings:ExpirationHours",        "1" },
                { "DefaultCategories:0:Name",           "Gehalt" },
                { "DefaultCategories:0:Type",           "income" },
                { "DefaultCategories:0:Color",          "#1cc88a" },
                // Disable rate limiting so parallel integration tests never hit 429
                { "RateLimitSettings:AuthPermitLimit",  int.MaxValue.ToString() },
            });
        });

        // Only replace the connection factory for SQLite in-memory mode
        // (for PostgreSQL/MySQL the app's own factory from Program.cs is correct)
        if (IsSqliteInMemory)
        {
            builder.ConfigureServices(services =>
            {
                ServiceDescriptor? descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbConnectionFactory));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<IDbConnectionFactory>(_ => new InMemoryDbConnectionFactory(_dbName));
            });
        }
    }

    /// <summary>
    /// For SQLite in-memory: opens a persistent connection so the database survives
    /// across the multiple short-lived connections made by repositories.
    /// For PostgreSQL/MySQL: no-op (the database is persistent).
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsSqliteInMemory)
        {
            _keepAlive = new SqliteConnection($"Data Source={_dbName};Mode=Memory;Cache=Shared");
            await _keepAlive.OpenAsync();
        }
    }

    public new async Task DisposeAsync()
    {
        if (_keepAlive != null)
        {
            await _keepAlive.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}
