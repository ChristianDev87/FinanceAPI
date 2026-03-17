using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Auth;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceAPI.Tests.Integration;

[Collection("IntegrationTests")]
public class DualAuthIntegrationTests : IClassFixture<FinanceApiFactory>
{
    private readonly FinanceApiFactory _factory;

    public DualAuthIntegrationTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Registers a user and returns their JWT token.</summary>
    private async Task<string> RegisterAndGetTokenAsync(string username)
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            email = $"{username}@dualauth-test.com",
            password = "Password123!"
        });
        response.EnsureSuccessStatusCode();
        AuthResponse? result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    /// <summary>Creates an API key for an authenticated user and returns the plaintext key.</summary>
    private async Task<string> CreateApiKeyAsync(string jwt)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/profile/apikeys", new { name = "DualAuthTestKey" });
        response.EnsureSuccessStatusCode();
        ApiKeyCreatedResponse? result = await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>();
        return result!.Key;
    }

    [Fact]
    public async Task ApiKeyAuth_ValidKey_Returns200()
    {
        string jwt = await RegisterAndGetTokenAsync("dualauth_apikey");
        string apiKey = await CreateApiKeyAsync(jwt);

        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        HttpResponseMessage response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyAuth_InvalidKey_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "invalid-key-that-does-not-exist");
        HttpResponseMessage response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task JwtAuth_ValidToken_Returns200()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "dualauth_jwt");
        HttpResponseMessage response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BothCredentials_ValidJwtWithInvalidApiKey_JwtTakesPriority_Returns200()
    {
        // If Authorization header is present, X-Api-Key is ignored entirely.
        // A valid JWT + an invalid/garbage API key must still succeed.
        string jwt = await RegisterAndGetTokenAsync("dualauth_priority");

        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        client.DefaultRequestHeaders.Add("X-Api-Key", "garbage-key-should-be-ignored");
        HttpResponseMessage response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyAuth_DeactivatedUser_Returns401()
    {
        string jwt = await RegisterAndGetTokenAsync("dualauth_deactivate");
        string apiKey = await CreateApiKeyAsync(jwt);

        // Sanity check: API key works before deactivation
        HttpClient beforeClient = _factory.CreateClient();
        beforeClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        HttpResponseMessage before = await beforeClient.GetAsync("/api/categories");
        before.EnsureSuccessStatusCode();

        // Deactivate the user directly via repository
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        User? user = await userRepo.GetByUsernameAsync("dualauth_deactivate");
        await userRepo.SetActiveAsync(user!.Id, false);

        // Deactivated user's API key must now be rejected
        HttpClient afterClient = _factory.CreateClient();
        afterClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        HttpResponseMessage after = await afterClient.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }
}
