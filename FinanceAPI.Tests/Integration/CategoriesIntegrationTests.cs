using System.Net;
using System.Net.Http.Json;
using FinanceAPI.DTOs.Categories;

namespace FinanceAPI.Tests.Integration;

[Collection("IntegrationTests")]
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
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_getall");

        HttpResponseMessage response = await client.GetAsync("/api/categories");

        response.EnsureSuccessStatusCode();
        List<CategoryDto>? categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.NotNull(categories);
        // Default categories are created on registration
        Assert.True(categories.Count >= 1);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns200WithDto()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_create");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "Freizeit",
            color = "#3498db",
            type = "expense",
            sortOrder = 10
        });

        response.EnsureSuccessStatusCode();
        CategoryDto? cat = await response.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.NotNull(cat);
        Assert.Equal("Freizeit", cat.Name);
        Assert.Equal("#3498db", cat.Color);
        Assert.Equal("expense", cat.Type);
        Assert.True(cat.Id > 0);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "Test",
            color = "#ffffff",
            type = "expense",
            sortOrder = 0
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_OwnCategory_Returns200WithUpdatedData()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_update");

        HttpResponseMessage createResp = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "OldName",
            color = "#ffffff",
            type = "expense",
            sortOrder = 0
        });
        createResp.EnsureSuccessStatusCode();
        CategoryDto? created = await createResp.Content.ReadFromJsonAsync<CategoryDto>();

        HttpResponseMessage updateResp = await client.PutAsJsonAsync($"/api/categories/{created!.Id}", new
        {
            name = "NewName",
            color = "#000000",
            type = "income",
            sortOrder = 5
        });

        updateResp.EnsureSuccessStatusCode();
        CategoryDto? updated = await updateResp.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.Equal("NewName", updated!.Name);
        Assert.Equal("income", updated.Type);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task Update_NonExistentCategory_Returns404()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_update404");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/categories/99999", new
        {
            name = "X",
            color = "#ffffff",
            type = "expense",
            sortOrder = 0
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OwnEmptyCategory_Returns204()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_delete");

        HttpResponseMessage createResp = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "ToDelete",
            color = "#aabbcc",
            type = "expense",
            sortOrder = 99
        });
        createResp.EnsureSuccessStatusCode();
        CategoryDto? created = await createResp.Content.ReadFromJsonAsync<CategoryDto>();

        HttpResponseMessage deleteResp = await client.DeleteAsync($"/api/categories/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task Delete_CategoryWithTransactions_Returns400()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_delete_blocked");

        // Create a category
        HttpResponseMessage createCatResp = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "HasTransactions",
            color = "#ffffff",
            type = "expense",
            sortOrder = 0
        });
        createCatResp.EnsureSuccessStatusCode();
        CategoryDto? cat = await createCatResp.Content.ReadFromJsonAsync<CategoryDto>();

        // Attach a transaction
        await client.PostAsJsonAsync("/api/transactions", new
        {
            amount = 50m,
            type = "expense",
            date = "2026-01-01",
            categoryId = cat!.Id
        });

        // Try to delete the category
        HttpResponseMessage deleteResp = await client.DeleteAsync($"/api/categories/{cat.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, deleteResp.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_dup");

        await client.PostAsJsonAsync("/api/categories", new
        {
            name = "Duplicate",
            color = "#aabbcc",
            type = "expense",
            sortOrder = 0
        });

        HttpResponseMessage second = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "Duplicate",
            color = "#112233",
            type = "expense",
            sortOrder = 1
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateNameCaseInsensitive_Returns409()
    {
        HttpClient client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_dup_ci");

        await client.PostAsJsonAsync("/api/categories", new
        {
            name = "Food",
            color = "#aabbcc",
            type = "expense",
            sortOrder = 0
        });

        HttpResponseMessage second = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "food",
            color = "#112233",
            type = "expense",
            sortOrder = 1
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherUsersCategory_Returns403()
    {
        HttpClient owner = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_owner");
        HttpClient intruder = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "cat_intruder");

        // Owner creates a category
        HttpResponseMessage createResp = await owner.PostAsJsonAsync("/api/categories", new
        {
            name = "Private",
            color = "#ffffff",
            type = "expense",
            sortOrder = 0
        });
        createResp.EnsureSuccessStatusCode();
        CategoryDto? cat = await createResp.Content.ReadFromJsonAsync<CategoryDto>();

        // Intruder tries to delete it
        HttpResponseMessage deleteResp = await intruder.DeleteAsync($"/api/categories/{cat!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, deleteResp.StatusCode);
    }
}
