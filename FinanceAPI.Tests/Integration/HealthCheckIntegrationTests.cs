using System.Net;

namespace FinanceAPI.Tests.Integration;

[Collection("IntegrationTests")]
public class HealthCheckIntegrationTests : IClassFixture<FinanceApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckIntegrationTests(FinanceApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        HttpResponseMessage response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }
}
