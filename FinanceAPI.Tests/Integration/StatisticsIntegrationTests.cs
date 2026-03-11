using System.Net.Http.Json;
using FinanceAPI.DTOs.Statistics;

namespace FinanceAPI.Tests.Integration;

public class StatisticsIntegrationTests : IClassFixture<FinanceApiFactory>
{
    private readonly FinanceApiFactory _factory;

    public StatisticsIntegrationTests(FinanceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetYears_NoTransactions_ReturnsEmptyList()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "stats_years1");

        var response = await client.GetAsync("/api/statistics/years");

        response.EnsureSuccessStatusCode();
        var years = await response.Content.ReadFromJsonAsync<List<int>>();
        Assert.NotNull(years);
        Assert.Empty(years);
    }

    [Fact]
    public async Task GetYears_WithTransactions_ReturnsDistinctYearsDescending()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "stats_years2");

        await client.PostAsJsonAsync("/api/transactions", new { amount = 100m, type = "expense", date = "2026-01-01" });
        await client.PostAsJsonAsync("/api/transactions", new { amount = 200m, type = "income",  date = "2025-06-15" });
        await client.PostAsJsonAsync("/api/transactions", new { amount = 50m,  type = "expense", date = "2026-03-10" });

        var response = await client.GetAsync("/api/statistics/years");

        response.EnsureSuccessStatusCode();
        var years = await response.Content.ReadFromJsonAsync<List<int>>();
        Assert.NotNull(years);
        Assert.Contains(2026, years);
        Assert.Contains(2025, years);
        Assert.Equal(2026, years[0]); // descending order
        Assert.Equal(2025, years[1]);
    }

    [Fact]
    public async Task GetMonthly_NoTransactions_Returns12MonthsWithZeros()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "stats_monthly1");

        var response = await client.GetAsync("/api/statistics/monthly?year=2026");

        response.EnsureSuccessStatusCode();
        var months = await response.Content.ReadFromJsonAsync<List<MonthlyStatDto>>();
        Assert.NotNull(months);
        Assert.Equal(12, months.Count);
        Assert.All(months, m =>
        {
            Assert.Equal(0m, m.TotalIncome);
            Assert.Equal(0m, m.TotalExpense);
        });
    }

    [Fact]
    public async Task GetMonthly_WithTransactions_ReturnsCorrectTotals()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "stats_monthly2");

        await client.PostAsJsonAsync("/api/transactions", new { amount = 1500m, type = "income",  date = "2026-03-01" });
        await client.PostAsJsonAsync("/api/transactions", new { amount = 300m,  type = "expense", date = "2026-03-05" });
        await client.PostAsJsonAsync("/api/transactions", new { amount = 200m,  type = "expense", date = "2026-03-20" });

        var response = await client.GetAsync("/api/statistics/monthly?year=2026");

        response.EnsureSuccessStatusCode();
        var months = await response.Content.ReadFromJsonAsync<List<MonthlyStatDto>>();
        Assert.NotNull(months);

        var march = months.First(m => m.Month == 3);
        Assert.Equal(1500m, march.TotalIncome);
        Assert.Equal(500m,  march.TotalExpense);
    }

    [Fact]
    public async Task GetMonthly_DefaultsToCurrentYear_WhenNoYearProvided()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "stats_monthly3");

        var response = await client.GetAsync("/api/statistics/monthly");

        response.EnsureSuccessStatusCode();
        var months = await response.Content.ReadFromJsonAsync<List<MonthlyStatDto>>();
        Assert.NotNull(months);
        Assert.Equal(12, months.Count);
    }

    [Fact]
    public async Task GetByCategory_NoTransactions_ReturnsEmptyList()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "stats_cat1");

        var response = await client.GetAsync("/api/statistics/categories?month=1&year=2026&type=expense");

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<List<CategoryStatDto>>();
        Assert.NotNull(data);
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetByCategory_WithUncategorizedTransaction_ReturnsEntry()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "stats_cat2");

        await client.PostAsJsonAsync("/api/transactions", new
        {
            amount = 88m, type = "expense", date = "2026-03-15"
        });

        var response = await client.GetAsync("/api/statistics/categories?month=3&year=2026&type=expense");

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<List<CategoryStatDto>>();
        Assert.NotNull(data);
        Assert.Single(data);
        Assert.Equal(88m, data[0].Total);
        Assert.Equal(1,   data[0].Count);
    }

    [Fact]
    public async Task GetByCategory_FiltersByType_ReturnsOnlyRequestedType()
    {
        var client = await TestHelpers.CreateAuthenticatedClientAsync(_factory, "stats_cat3");

        await client.PostAsJsonAsync("/api/transactions", new { amount = 500m, type = "income",  date = "2026-04-01" });
        await client.PostAsJsonAsync("/api/transactions", new { amount = 100m, type = "expense", date = "2026-04-01" });

        var expenseResp = await client.GetAsync("/api/statistics/categories?month=4&year=2026&type=expense");
        expenseResp.EnsureSuccessStatusCode();
        var expenses = await expenseResp.Content.ReadFromJsonAsync<List<CategoryStatDto>>();
        Assert.NotNull(expenses);
        Assert.All(expenses!, e => Assert.Equal("expense", e.Type));

        var incomeResp = await client.GetAsync("/api/statistics/categories?month=4&year=2026&type=income");
        incomeResp.EnsureSuccessStatusCode();
        var incomes = await incomeResp.Content.ReadFromJsonAsync<List<CategoryStatDto>>();
        Assert.NotNull(incomes);
        Assert.All(incomes!, i => Assert.Equal("income", i.Type));
    }
}
