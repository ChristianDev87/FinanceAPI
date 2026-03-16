using System.Net;
using System.Net.Http.Json;
using FinanceAPI.DTOs.Auth;

namespace FinanceAPI.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<FinanceApiFactory>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(FinanceApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidRequest_Returns200WithToken()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "auth_reg1",
            email = "auth_reg1@test.com",
            password = "Password123!"
        });

        response.EnsureSuccessStatusCode();
        AuthResponse? result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result?.Token);
        Assert.NotEmpty(result.Token);
        Assert.Equal("Auth_reg1", result.User.Username);
        Assert.NotEmpty(result.User.Role);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns400()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "dup_user_auth",
            email = "dup_user_auth@test.com",
            password = "Password123!"
        });

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "dup_user_auth",
            email = "different_auth@test.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "email_orig_auth",
            email = "shared_email_auth@test.com",
            password = "Password123!"
        });

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "email_other_auth",
            email = "shared_email_auth@test.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "login_test_auth",
            email = "login_test_auth@test.com",
            password = "Password123!"
        });

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "login_test_auth",
            password = "Password123!"
        });

        response.EnsureSuccessStatusCode();
        AuthResponse? result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result?.Token);
        Assert.Equal("Login_test_auth", result.User.Username);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns404()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "wrongpass_auth",
            email = "wrongpass_auth@test.com",
            password = "Password123!"
        });

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "wrongpass_auth",
            password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns404()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "nobody_at_all",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
