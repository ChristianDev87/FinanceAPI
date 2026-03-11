using FinanceAPI.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceAPI.Tests.Integration;

/// <summary>
/// A WebApplicationFactory that runs the FinanceAPI against an isolated
/// named SQLite in-memory database. Each factory instance gets its own
/// database, so test classes that each receive their own factory instance
/// are fully isolated from each other.
/// </summary>
public sealed class FinanceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"finance_test_{Guid.NewGuid():N}";
    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Inject test-specific configuration (JWT settings, connection string, default categories)
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", $"Data Source={_dbName};Mode=Memory;Cache=Shared" },
                { "JwtSettings:SecretKey",              "super-secret-key-for-integration-tests-32chars!!" },
                { "JwtSettings:Issuer",                 "FinanceAPI-Test" },
                { "JwtSettings:Audience",               "FinanceAPI-Test" },
                { "JwtSettings:ExpirationHours",        "1" },
                { "DefaultCategories:0:Name",           "Gehalt" },
                { "DefaultCategories:0:Type",           "income" },
                { "DefaultCategories:0:Color",          "#1cc88a" },
            });
        });

        // Replace the real IDbConnectionFactory with the in-memory one
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbConnectionFactory));
            if (descriptor != null) services.Remove(descriptor);

            services.AddSingleton<IDbConnectionFactory>(_ => new InMemoryDbConnectionFactory(_dbName));
        });
    }

    /// <summary>
    /// Opens a persistent connection so the in-memory database survives
    /// across the multiple short-lived connections made by repositories.
    /// This must be called before CreateClient(), which triggers app startup
    /// and DatabaseInitializer.
    /// </summary>
    public async Task InitializeAsync()
    {
        _keepAlive = new SqliteConnection($"Data Source={_dbName};Mode=Memory;Cache=Shared");
        await _keepAlive.OpenAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_keepAlive != null)
            await _keepAlive.DisposeAsync();

        await base.DisposeAsync();
    }
}
