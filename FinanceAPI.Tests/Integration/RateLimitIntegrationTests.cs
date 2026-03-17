using System.Net;
using System.Net.Http.Json;
using FinanceAPI.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceAPI.Tests.Integration;

/// <summary>
/// A dedicated factory that enables a small auth rate limit so rate-limiting
/// behaviour can be verified without affecting the shared FinanceApiFactory
/// (which disables the limiter to avoid flaky parallel integration tests).
/// </summary>
public sealed class RateLimitFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"rl_test_{Guid.NewGuid():N}";
    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", $"Data Source={_dbName};Mode=Memory;Cache=Shared" },
                { "DatabaseSettings:Provider",           "sqlite" },
                { "JwtSettings:SecretKey",              "super-secret-key-for-integration-tests-32chars!!" },
                { "JwtSettings:Issuer",                 "FinanceAPI-Test" },
                { "JwtSettings:Audience",               "FinanceAPI-Test" },
                { "JwtSettings:ExpirationHours",        "1" },
                { "DefaultCategories:0:Name",           "Gehalt" },
                { "DefaultCategories:0:Type",           "income" },
                { "DefaultCategories:0:Color",          "#1cc88a" },
                // Small limit so tests can exhaust it quickly
                { "RateLimitSettings:AuthPermitLimit",  "3" },
                { "RateLimitSettings:AuthWindowMinutes", "1" },
            });
        });

        builder.ConfigureServices(services =>
        {
            ServiceDescriptor? descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDbConnectionFactory));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IDbConnectionFactory>(_ => new InMemoryDbConnectionFactory(_dbName));
        });
    }

    public async Task InitializeAsync()
    {
        _keepAlive = new SqliteConnection($"Data Source={_dbName};Mode=Memory;Cache=Shared");
        await _keepAlive.OpenAsync();
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

// Not part of the shared IntegrationTests collection — uses its own isolated factory.
public class RateLimitIntegrationTests : IClassFixture<RateLimitFactory>
{
    private readonly RateLimitFactory _factory;

    public RateLimitIntegrationTests(RateLimitFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthEndpoint_ExceedsLimit_Returns429()
    {
        // The factory configures AuthPermitLimit = 3.
        // Sending 4 requests from the same IP (all "unknown" in TestServer)
        // must result in at least one 429.
        HttpClient client = _factory.CreateClient();

        List<HttpStatusCode> results = new List<HttpStatusCode>();
        for (int i = 0; i < 4; i++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                username = "nobody",
                password = "irrelevant"
            });
            results.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, results);
    }

    [Fact]
    public async Task AuthEndpoint_WithinLimit_AllRequestsReach401()
    {
        // Exactly 3 requests (= AuthPermitLimit) must all reach the handler
        // and return 401 (wrong credentials), not 429.
        HttpClient client = _factory.CreateClient();

        for (int i = 0; i < 3; i++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                username = "nobody",
                password = "irrelevant"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task NonAuthEndpoint_NotSubjectToAuthRateLimit_Returns401NotLimited()
    {
        // The "auth" policy is only applied to /api/auth/* endpoints.
        // Unauthenticated calls to other endpoints must never hit 429.
        HttpClient client = _factory.CreateClient();

        for (int i = 0; i < 10; i++)
        {
            HttpResponseMessage response = await client.GetAsync("/api/categories");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
