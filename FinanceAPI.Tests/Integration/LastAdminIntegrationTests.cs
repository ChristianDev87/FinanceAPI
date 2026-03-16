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
    public async Task ConcurrentDeactivation_AdminInvariantNeverBroken()
    {
        // Two admins each try to deactivate the other simultaneously.
        // Either the SemaphoreSlim serialises them (one gets 400 — last admin) or the
        // OnTokenValidated check rejects the second request (401 — account just deactivated).
        // Either way: at most one deactivation can succeed and the DB must keep at least 1 active admin.
        (HttpClient admin1Client, List<int> deactivatedAdminIds) = await SetupSoleAdminAsync("p2_conc1");
        try
        {
            HttpClient admin2Client = _factory.CreateClient();
            await admin2Client.PostAsJsonAsync("/api/auth/register", new
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

            HttpResponseMessage login2 = await admin2Client.PostAsJsonAsync("/api/auth/login", new
            {
                username = "p2_conc2",
                password = "Password123!"
            });
            AuthResponse? loginResult2 = await login2.Content.ReadFromJsonAsync<AuthResponse>();
            admin2Client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult2!.Token);

            User? admin1 = await userRepo.GetByUsernameAsync("p2_conc1");

            // Both admins attempt to deactivate each other at the same time
            Task<HttpResponseMessage> req1 = admin1Client.PutAsJsonAsync($"/api/users/{admin2.Id}/active", false);
            Task<HttpResponseMessage> req2 = admin2Client.PutAsJsonAsync($"/api/users/{admin1!.Id}/active", false);

            HttpResponseMessage[] results = await Task.WhenAll(req1, req2);

            // At most one deactivation can succeed; the other must be rejected for any reason
            int successCount = results.Count(r => r.IsSuccessStatusCode);
            Assert.True(successCount <= 1, $"Both concurrent deactivations succeeded — the last-admin invariant may be broken.");

            // The DB must always retain at least one active admin
            int remaining = await userRepo.CountActiveAdminsAsync();
            Assert.True(remaining >= 1, $"Expected at least 1 active admin after concurrent deactivation, found {remaining}.");
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
