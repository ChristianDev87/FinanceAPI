using System.Net;
using System.Net.Http.Json;
using FinanceAPI.DTOs.Categories;

namespace FinanceAPI.Tests.Integration;

public class CategoriesIntegrationTests : IClassFixture<FinanceApiFactory>
{
    private readonly FinanceApiFactory _factory;

    public CategoriesIntegrationTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_Authenticated_Returns200()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_getall");

        var response = await client.GetAsync("/api/categories");

        response.EnsureSuccessStatusCode();
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.NotNull(categories);
        // Default categories are created on registration
        Assert.True(categories.Count >= 1);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns200WithDto()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_create");

        var response = await client.PostAsJsonAsync("/api/categories", new
        {
            name      = "Freizeit",
            color     = "#3498db",
            type      = "expense",
            sortOrder = 10
        });

        response.EnsureSuccessStatusCode();
        var cat = await response.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.NotNull(cat);
        Assert.Equal("Freizeit", cat.Name);
        Assert.Equal("#3498db",  cat.Color);
        Assert.Equal("expense",  cat.Type);
        Assert.True(cat.Id > 0);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "Test", color = "#fff", type = "expense", sortOrder = 0
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_OwnCategory_Returns200WithUpdatedData()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_update");

        var createResp = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "OldName", color = "#fff", type = "expense", sortOrder = 0
        });
        var created = await createResp.Content.ReadFromJsonAsync<CategoryDto>();

        var updateResp = await client.PutAsJsonAsync($"/api/categories/{created!.Id}", new
        {
            name = "NewName", color = "#000", type = "income", sortOrder = 5
        });

        updateResp.EnsureSuccessStatusCode();
        var updated = await updateResp.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.Equal("NewName", updated!.Name);
        Assert.Equal("income",  updated.Type);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task Update_NonExistentCategory_Returns404()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_update404");

        var response = await client.PutAsJsonAsync("/api/categories/99999", new
        {
            name = "X", color = "#fff", type = "expense", sortOrder = 0
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OwnEmptyCategory_Returns204()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_delete");

        var createResp = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "ToDelete", color = "#abc", type = "expense", sortOrder = 99
        });
        var created = await createResp.Content.ReadFromJsonAsync<CategoryDto>();

        var deleteResp = await client.DeleteAsync($"/api/categories/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task Delete_CategoryWithTransactions_Returns400()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_delete_blocked");

        // Create a category
        var createCatResp = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "HasTransactions", color = "#fff", type = "expense", sortOrder = 0
        });
        var cat = await createCatResp.Content.ReadFromJsonAsync<CategoryDto>();

        // Attach a transaction
        await client.PostAsJsonAsync("/api/transactions", new
        {
            amount     = 50m,
            type       = "expense",
            date       = "2026-01-01",
            categoryId = cat!.Id
        });

        // Try to delete the category
        var deleteResp = await client.DeleteAsync($"/api/categories/{cat.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, deleteResp.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherUsersCategory_Returns401()
    {
        var owner   = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_owner");
        var intruder = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_intruder");

        // Owner creates a category
        var createResp = await owner.PostAsJsonAsync("/api/categories", new
        {
            name = "Private", color = "#fff", type = "expense", sortOrder = 0
        });
        var cat = await createResp.Content.ReadFromJsonAsync<CategoryDto>();

        // Intruder tries to delete it
        var deleteResp = await intruder.DeleteAsync($"/api/categories/{cat!.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, deleteResp.StatusCode);
    }
}
