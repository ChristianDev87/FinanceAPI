using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceAPI.DTOs.Auth;

namespace FinanceAPI.Tests.Integration;

public static class TestHelpers
{
    /// <summary>
    /// Creates an HttpClient that is already authenticated as the given user.
    /// Registers the user if they don't exist yet, then logs in and attaches the JWT.
    /// </summary>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        FinanceApiFactory factory,
        string username,
        string password = "Password123!")
    {
        var client = factory.CreateClient();

        // Try to register; if the user already exists we fall through to login
        var regResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            email    = $"{username}@integration-test.com",
            password
        });

        string token;

        if (regResponse.IsSuccessStatusCode)
        {
            var regResult = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
            token = regResult!.Token;
        }
        else
        {
            // User already exists — log in
            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                username,
                password
            });
            loginResponse.EnsureSuccessStatusCode();
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            token = loginResult!.Token;
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
