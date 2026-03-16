using System.Net;
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
}
