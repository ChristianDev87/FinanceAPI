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
public class SecurityRegressionTests : IClassFixture<FinanceApiFactory>
{
    private readonly FinanceApiFactory _factory;

    public SecurityRegressionTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    // ── JWT Invalidation after Password Change ────────────────────────────────

    [Fact]
    public async Task ChangePassword_OldJwt_IsRejectedWith401()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "sec_pwchange");

        // Sanity check: JWT works before password change
        (await client.GetAsync("/api/categories")).EnsureSuccessStatusCode();

        // Change password via profile endpoint
        HttpResponseMessage pwResp = await client.PutAsJsonAsync("/api/profile/password", new
        {
            currentPassword = "Password123!",
            newPassword = "NewPassword456!"
        });
        pwResp.EnsureSuccessStatusCode();

        // Old JWT must now be rejected
        HttpResponseMessage after = await client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task AdminSetPassword_OldJwtOfTargetUser_IsRejectedWith401()
    {
        // Register and authenticate target user
        HttpClient userClient = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "sec_adminpw_user");

        // Sanity check: token works
        (await userClient.GetAsync("/api/categories")).EnsureSuccessStatusCode();

        // Register admin user and promote via repository
        HttpClient adminClient = _factory.CreateClient();
        await adminClient.PostAsJsonAsync("/api/auth/register", new
        {
            username = "sec_adminpw_admin",
            email = "sec_adminpw_admin@integration-test.com",
            password = "Password123!"
        });

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            User? adminUser = await userRepo.GetByUsernameAsync("sec_adminpw_admin");
            if (adminUser!.RoleName != "Admin")
            {
                adminUser.RoleName = "Admin";
                await userRepo.UpdateAsync(adminUser);
            }
        }

        // Login as admin (to pick up the promoted role in the JWT)
        HttpResponseMessage adminLogin = await adminClient.PostAsJsonAsync("/api/auth/login", new
        {
            username = "sec_adminpw_admin",
            password = "Password123!"
        });
        adminLogin.EnsureSuccessStatusCode();
        AuthResponse? adminAuth = await adminLogin.Content.ReadFromJsonAsync<AuthResponse>();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminAuth!.Token);

        // Look up the target user's ID
        int targetUserId;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            User? target = await userRepo.GetByUsernameAsync("sec_adminpw_user");
            targetUserId = target!.Id;
        }

        // Admin resets target's password
        (await adminClient.PutAsJsonAsync($"/api/users/{targetUserId}/password",
            new { newPassword = "AdminReset789!" })).EnsureSuccessStatusCode();

        // Target user's old JWT must now be rejected
        HttpResponseMessage after = await userClient.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    // ── API Key Rotation Concurrency ──────────────────────────────────────────

    [Fact]
    public async Task ConcurrentApiKeyCreation_AtMostOneActiveKeyExists()
    {
        // Register user and obtain a token for parallel requests
        HttpClient seedClient = _factory.CreateClient();
        HttpResponseMessage regResp = await seedClient.PostAsJsonAsync("/api/auth/register", new
        {
            username = "sec_apikey_conc",
            email = "sec_apikey_conc@integration-test.com",
            password = "Password123!"
        });

        string token;
        if (regResp.IsSuccessStatusCode)
        {
            token = (await regResp.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
        }
        else
        {
            HttpResponseMessage loginResp = await seedClient.PostAsJsonAsync("/api/auth/login", new
            {
                username = "sec_apikey_conc",
                password = "Password123!"
            });
            loginResp.EnsureSuccessStatusCode();
            token = (await loginResp.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
        }

        // Fire 5 parallel CreateApiKey requests with the same user's JWT
        Task<HttpResponseMessage>[] tasks = Enumerable.Range(0, 5).Select(_ =>
        {
            HttpClient c = _factory.CreateClient();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return c.PostAsJsonAsync("/api/profile/apikeys", new { name = "ConcKey" });
        }).ToArray();

        await Task.WhenAll(tasks);

        // Verify that exactly one active API key exists for this user
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IApiKeyRepository apiKeyRepo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

        User? user = await userRepo.GetByUsernameAsync("sec_apikey_conc");
        IEnumerable<ApiKey> keys = await apiKeyRepo.GetByUserIdAsync(user!.Id);
        int activeCount = keys.Count(k => k.IsActive);

        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task ApiKeyRotation_NewKeyWorks_OldKeyRevoked()
    {
        // Create user and first API key
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "sec_apikey_rotate");
        HttpResponseMessage firstResp = await client.PostAsJsonAsync("/api/profile/apikeys", new { name = "Key1" });
        firstResp.EnsureSuccessStatusCode();
        ApiKeyCreatedResponse? first = await firstResp.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>();

        // Verify first key works
        HttpClient keyClient = _factory.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-Api-Key", first!.Key);
        (await keyClient.GetAsync("/api/categories")).EnsureSuccessStatusCode();

        // Create a second key — this should deactivate the first
        HttpResponseMessage secondResp = await client.PostAsJsonAsync("/api/profile/apikeys", new { name = "Key2" });
        secondResp.EnsureSuccessStatusCode();
        ApiKeyCreatedResponse? second = await secondResp.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>();

        // Old key must now be rejected
        HttpResponseMessage oldKeyResp = await keyClient.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, oldKeyResp.StatusCode);

        // New key must work
        HttpClient newKeyClient = _factory.CreateClient();
        newKeyClient.DefaultRequestHeaders.Add("X-Api-Key", second!.Key);
        (await newKeyClient.GetAsync("/api/categories")).EnsureSuccessStatusCode();
    }
}
