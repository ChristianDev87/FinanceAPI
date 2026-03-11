using System.Net;
using System.Net.Http.Json;
using FinanceAPI.DTOs.Transactions;

namespace FinanceAPI.Tests.Integration;

public class TransactionsIntegrationTests : IClassFixture<FinanceApiFactory>
{
    private readonly FinanceApiFactory _factory;

    public TransactionsIntegrationTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_Authenticated_Returns200()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_getall");

        var response = await client.GetAsync("/api/transactions");

        response.EnsureSuccessStatusCode();
        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        Assert.NotNull(transactions);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/transactions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidTransaction_Returns200WithDto()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_create");

        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            amount      = 99.50m,
            type        = "expense",
            date        = "2026-03-01",
            description = "Test purchase"
        });

        response.EnsureSuccessStatusCode();
        var tx = await response.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.NotNull(tx);
        Assert.Equal(99.50m,  tx.Amount);
        Assert.Equal("expense", tx.Type);
        Assert.Equal("Test purchase", tx.Description);
        Assert.True(tx.Id > 0);
    }

    [Fact]
    public async Task Create_WithCategory_AttachesCategoryInfo()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_create_cat");

        // Create a category first
        var catResp = await client.PostAsJsonAsync("/api/categories", new
        {
            name = "Groceries", color = "#27ae60", type = "expense", sortOrder = 0
        });
        var cat = await catResp.Content.ReadFromJsonAsync<FinanceAPI.DTOs.Categories.CategoryDto>();

        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            amount     = 45.00m,
            type       = "expense",
            date       = "2026-03-10",
            categoryId = cat!.Id
        });

        response.EnsureSuccessStatusCode();
        var tx = await response.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.Equal(cat.Id,       tx!.CategoryId);
        Assert.Equal("Groceries",  tx.CategoryName);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            amount = 10m, type = "expense", date = "2026-01-01"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_OwnTransaction_Returns200()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_getbyid");

        var createResp = await client.PostAsJsonAsync("/api/transactions", new
        {
            amount = 77m, type = "income", date = "2026-02-15"
        });
        var created = await createResp.Content.ReadFromJsonAsync<TransactionDto>();

        var response = await client.GetAsync($"/api/transactions/{created!.Id}");

        response.EnsureSuccessStatusCode();
        var tx = await response.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.Equal(77m,    tx!.Amount);
        Assert.Equal("income", tx.Type);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_getbyid404");

        var response = await client.GetAsync("/api/transactions/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_OtherUsersTransaction_Returns401()
    {
        var owner   = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_owner");
        var intruder = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_intruder");

        var createResp = await owner.PostAsJsonAsync("/api/transactions", new
        {
            amount = 200m, type = "income", date = "2026-01-01"
        });
        var created = await createResp.Content.ReadFromJsonAsync<TransactionDto>();

        var response = await intruder.GetAsync($"/api/transactions/{created!.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_OwnTransaction_Returns200WithUpdatedData()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_update");

        var createResp = await client.PostAsJsonAsync("/api/transactions", new
        {
            amount = 50m, type = "expense", date = "2026-01-10"
        });
        var created = await createResp.Content.ReadFromJsonAsync<TransactionDto>();

        var updateResp = await client.PutAsJsonAsync($"/api/transactions/{created!.Id}", new
        {
            amount      = 75m,
            type        = "expense",
            date        = "2026-01-15",
            description = "Updated"
        });

        updateResp.EnsureSuccessStatusCode();
        var updated = await updateResp.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.Equal(75m,      updated!.Amount);
        Assert.Equal("Updated", updated.Description);
    }

    [Fact]
    public async Task Delete_OwnTransaction_Returns204()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_delete");

        var createResp = await client.PostAsJsonAsync("/api/transactions", new
        {
            amount = 25m, type = "expense", date = "2026-01-20"
        });
        var created = await createResp.Content.ReadFromJsonAsync<TransactionDto>();

        var deleteResp = await client.DeleteAsync($"/api/transactions/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Verify it's gone
        var getResp = await client.GetAsync($"/api/transactions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherUsersTransaction_Returns401()
    {
        var owner   = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_del_owner");
        var intruder = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_del_intruder");

        var createResp = await owner.PostAsJsonAsync("/api/transactions", new
        {
            amount = 100m, type = "income", date = "2026-03-01"
        });
        var created = await createResp.Content.ReadFromJsonAsync<TransactionDto>();

        var deleteResp = await intruder.DeleteAsync($"/api/transactions/{created!.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, deleteResp.StatusCode);
    }

    [Fact]
    public async Task GetAll_FilterByMonthAndYear_ReturnsOnlyMatchingTransactions()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_filter");

        await client.PostAsJsonAsync("/api/transactions", new { amount = 100m, type = "expense", date = "2026-01-15" });
        await client.PostAsJsonAsync("/api/transactions", new { amount = 200m, type = "income",  date = "2026-02-20" });
        await client.PostAsJsonAsync("/api/transactions", new { amount = 300m, type = "expense", date = "2025-01-01" });

        var response = await client.GetAsync("/api/transactions?month=1&year=2026");

        response.EnsureSuccessStatusCode();
        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        Assert.NotNull(transactions);
        Assert.All(transactions!, tx => Assert.StartsWith("2026-01", tx.Date));
    }

    [Fact]
    public async Task GetAll_FilterByType_ReturnsOnlyMatchingType()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "tx_filter_type");

        await client.PostAsJsonAsync("/api/transactions", new { amount = 50m,  type = "expense", date = "2026-03-01" });
        await client.PostAsJsonAsync("/api/transactions", new { amount = 150m, type = "income",  date = "2026-03-02" });

        var response = await client.GetAsync("/api/transactions?type=income&month=3&year=2026");

        response.EnsureSuccessStatusCode();
        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        Assert.NotNull(transactions);
        Assert.All(transactions!, tx => Assert.Equal("income", tx.Type));
    }
}
