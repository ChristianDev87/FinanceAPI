using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceAPI.DTOs.Auth;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceAPI.Tests.Integration;

[Collection("IntegrationTests")]
public class UserStatusIntegrationTests : IClassFixture<FinanceApiFactory>
{
    private readonly FinanceApiFactory _factory;

    public UserStatusIntegrationTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeactivatedUser_WithValidJwt_Returns401()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "status_deactivate");

        // Sanity check: JWT works before deactivation
        HttpResponseMessage before = await client.GetAsync("/api/categories");
        before.EnsureSuccessStatusCode();

        // Deactivate the user directly via repository
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        User? user = await userRepo.GetByUsernameAsync("status_deactivate");
        await userRepo.SetActiveAsync(user!.Id, false);

        // Old JWT must be rejected immediately
        HttpResponseMessage response = await client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeletedUser_WithValidJwt_Returns401()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "status_delete");

        // Sanity check: JWT works before deletion
        HttpResponseMessage before = await client.GetAsync("/api/categories");
        before.EnsureSuccessStatusCode();

        // Delete the user directly via repository
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserRepository userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        User? user = await userRepo.GetByUsernameAsync("status_delete");
        await userRepo.DeleteAsync(user!.Id);

        // Old JWT must be rejected immediately
        HttpResponseMessage response = await client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DemotedAdmin_OldJwt_CannotAccessAdminEndpoints_Returns403()
    {
        HttpClient client = _factory.CreateClient();

        // Register and log in to get an admin-role JWT
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "status_demote",
            email = "status_demote@integration-test.com",
            password = "Password123!"
        });

        using IServiceScope promoteScope = _factory.Services.CreateScope();
        IUserRepository promoteRepo = promoteScope.ServiceProvider.GetRequiredService<IUserRepository>();
        User? adminUser = await promoteRepo.GetByUsernameAsync("status_demote");
        adminUser!.RoleName = "Admin";
        await promoteRepo.UpdateAsync(adminUser);

        // Log in after promotion so the JWT carries the Admin role
        HttpResponseMessage loginResp = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "status_demote",
            password = "Password123!"
        });
        loginResp.EnsureSuccessStatusCode();
        AuthResponse? loginResult = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult!.Token);

        // Sanity check: admin JWT reaches /api/users
        HttpResponseMessage before = await client.GetAsync("/api/users");
        before.EnsureSuccessStatusCode();

        // Demote the user to User role directly via repository (simulates another admin doing it)
        using IServiceScope demoteScope = _factory.Services.CreateScope();
        IUserRepository demoteRepo = demoteScope.ServiceProvider.GetRequiredService<IUserRepository>();
        User? demoted = await demoteRepo.GetByUsernameAsync("status_demote");
        demoted!.RoleName = "User";
        await demoteRepo.UpdateAsync(demoted);

        // Old JWT must now be refused on admin endpoints
        HttpResponseMessage response = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
