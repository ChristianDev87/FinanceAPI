using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceAPI.DTOs.Auth;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceAPI.Tests.Integration;

[Collection("IntegrationTests")]
public class LastAdminIntegrationTests : IClassFixture<FinanceApiFactory>
{
    private readonly FinanceApiFactory _factory;

    public LastAdminIntegrationTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Registers (or logs in) the given user, ensures they have the Admin role,
    /// and temporarily deactivates all other active admins so the user is the
    /// sole active admin. Returns the IDs that were deactivated for cleanup.
    /// </summary>
    private async Task<(HttpClient client, List<int> deactivatedAdminIds)> SetupSoleAdminAsync(string username)
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage regResp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username,
            email = $"{username}@integration-test.com",
            password = "Password123!"
        });

        // We don't need the token from registration — we'll issue a fresh login below
        // once we've guaranteed the admin role.

        using IServiceScope scope = _factory.Services.CreateScope();
        IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        User? user = await userRepo.GetByUsernameAsync(username);

        if (user!.RoleName != "Admin")
        {
            user.RoleName = "Admin";
            await userRepo.UpdateAsync(user);
        }

        // Login to get a token that carries the Admin role claim
        HttpResponseMessage loginResp = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password = "Password123!"
        });
        loginResp.EnsureSuccessStatusCode();
        AuthResponse? loginResult = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token);

        // Deactivate every other active admin so the test user is the sole admin
        IEnumerable<User> allUsers = await userRepo.GetAllAsync();
        List<int> deactivatedAdminIds = allUsers
            .Where(u => u.RoleName == "Admin" && u.IsActive && u.Id != user.Id)
            .Select(u => u.Id)
            .ToList();

        foreach (int id in deactivatedAdminIds)
        {
            await userRepo.SetActiveAsync(id, false);
        }

        return (client, deactivatedAdminIds);
    }

    private async Task ReactivateAdminsAsync(List<int> adminIds)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        foreach (int id in adminIds)
        {
            await userRepo.SetActiveAsync(id, true);
        }
    }

    [Fact]
    public async Task LastAdmin_CannotDemoteSelf_Returns400()
    {
        (HttpClient? client, List<int>? deactivatedAdminIds) = await SetupSoleAdminAsync("p2_demote");
        try
        {
            HttpResponseMessage profileResp = await client.GetAsync("/api/profile");
            profileResp.EnsureSuccessStatusCode();
            UserDto? profile = await profileResp.Content.ReadFromJsonAsync<UserDto>();

            HttpResponseMessage response = await client.PutAsJsonAsync($"/api/users/{profile!.Id}", new
            {
                username = profile.Username,
                email = profile.Email,
                role = "User"
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await ReactivateAdminsAsync(deactivatedAdminIds);
        }
    }

    [Fact]
    public async Task LastAdmin_CannotDeleteOwnAccount_Returns400()
    {
        (HttpClient? client, List<int>? deactivatedAdminIds) = await SetupSoleAdminAsync("p2_delete");
        try
        {
            HttpResponseMessage response = await client.DeleteAsync("/api/profile");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await ReactivateAdminsAsync(deactivatedAdminIds);
        }
    }

    [Fact]
    public async Task ConcurrentDeactivation_OnlyOneSucceeds_AdminNeverLost()
    {
        // Set up two active admins: admin1 (authenticated client) and admin2 (the target).
        // admin1 sends two concurrent requests to deactivate admin2.
        // With only 2 active admins the SemaphoreSlim ensures:
        //   - first request: count = 2 → deactivates admin2 → 204
        //   - second request: count = 1 → throws 400 (last admin protection)
        (HttpClient admin1Client, List<int> deactivatedAdminIds) = await SetupSoleAdminAsync("p2_conc1");
        try
        {
            await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
            {
                username = "p2_conc2",
                email = "p2_conc2@integration-test.com",
                password = "Password123!"
            });

            using IServiceScope scope = _factory.Services.CreateScope();
            IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            User? admin2 = await userRepo.GetByUsernameAsync("p2_conc2");
            admin2!.RoleName = "Admin";
            await userRepo.UpdateAsync(admin2);

            // Two concurrent deactivation requests targeting the same admin
            Task<HttpResponseMessage> req1 = admin1Client.PutAsJsonAsync($"/api/users/{admin2.Id}/active", false);
            Task<HttpResponseMessage> req2 = admin1Client.PutAsJsonAsync($"/api/users/{admin2.Id}/active", false);

            HttpResponseMessage[] results = await Task.WhenAll(req1, req2);

            int successCount = results.Count(r => r.IsSuccessStatusCode);
            int failCount = results.Count(r => r.StatusCode == System.Net.HttpStatusCode.BadRequest);

            // Exactly one must succeed and one must be rejected
            Assert.Equal(1, successCount);
            Assert.Equal(1, failCount);

            // DB must still have at least one active admin
            int remaining = await userRepo.CountActiveAdminsAsync();
            Assert.True(remaining >= 1, $"Expected at least 1 active admin, found {remaining}.");
        }
        finally
        {
            await ReactivateAdminsAsync(deactivatedAdminIds);
        }
    }

    [Fact]
    public async Task Demotion_AllowedWhenSecondAdminExists_Returns200()
    {
        (HttpClient? admin1Client, List<int>? deactivatedAdminIds) = await SetupSoleAdminAsync("p2_admin1");
        try
        {
            // Register a second user and promote them to admin
            await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
            {
                username = "p2_admin2",
                email = "p2_admin2@integration-test.com",
                password = "Password123!"
            });

            using IServiceScope scope = _factory.Services.CreateScope();
            IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            User? admin2 = await userRepo.GetByUsernameAsync("p2_admin2");
            if (admin2!.RoleName != "Admin")
            {
                admin2.RoleName = "Admin";
                await userRepo.UpdateAsync(admin2);
            }

            // p2_admin1 demotes p2_admin2 — allowed because p2_admin1 remains active admin
            HttpResponseMessage demoteResp = await admin1Client.PutAsJsonAsync(
                $"/api/users/{admin2.Id}", new
                {
                    username = admin2.Username,
                    email = admin2.Email,
                    role = "User"
                });

            Assert.Equal(HttpStatusCode.OK, demoteResp.StatusCode);
        }
        finally
        {
            await ReactivateAdminsAsync(deactivatedAdminIds);
        }
    }
}
