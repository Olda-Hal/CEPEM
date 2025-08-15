using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Text.Json;
using Xunit;
using DatabaseAPI.Models;

namespace DatabaseAPI.Tests.Integration;

public class DatabaseApiIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DatabaseApiIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Success()
    {
        var response = await _client.GetAsync("/api/health");
        
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var healthResponse = JsonSerializer.Deserialize<HealthResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(healthResponse);
        Assert.Equal("Healthy", healthResponse.Status);
        Assert.Equal("Database API", healthResponse.Service);
    }

    [Fact]
    public async Task Health_Endpoint_Contains_Correct_Fields()
    {
        var response = await _client.GetAsync("/api/health");
        var content = await response.Content.ReadAsStringAsync();
        var healthResponse = JsonSerializer.Deserialize<HealthResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(healthResponse);
        Assert.False(string.IsNullOrEmpty(healthResponse.Status));
        Assert.False(string.IsNullOrEmpty(healthResponse.Service));
        Assert.True(healthResponse.Timestamp > DateTime.MinValue);
        Assert.False(string.IsNullOrEmpty(healthResponse.Version));
    }
}
