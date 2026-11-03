using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DatabaseAPI.Controllers;
using DatabaseAPI.APIModels;
using DatabaseAPI.Data;

namespace DatabaseAPI.Tests.Controllers;

public class HealthControllerTests
{
    private static HealthController CreateController()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase($"health-{Guid.NewGuid()}")
            .Options;
        return new HealthController(new DatabaseContext(options));
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWhenDatabaseIsReachable()
    {
        var result = await CreateController().GetHealth();

        var okResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetHealth_ReturnsCorrectHealthData()
    {
        var result = await CreateController().GetHealth();
        var okResult = Assert.IsType<ObjectResult>(result.Result);
        var healthResponse = Assert.IsType<HealthResponse>(okResult.Value);

        Assert.Equal("Healthy", healthResponse.Status);
        Assert.Equal("Database API", healthResponse.Service);
        Assert.Equal("1.0.0", healthResponse.Version);
    }

    [Fact]
    public async Task GetHealth_TimestampIsRecent()
    {
        var beforeTest = DateTime.UtcNow;

        var result = await CreateController().GetHealth();
        var okResult = Assert.IsType<ObjectResult>(result.Result);
        var healthResponse = Assert.IsType<HealthResponse>(okResult.Value);

        var afterTest = DateTime.UtcNow;
        Assert.True(healthResponse.Timestamp >= beforeTest);
        Assert.True(healthResponse.Timestamp <= afterTest);
    }

    [Fact]
    public void GetLiveness_ReturnsOkWithoutDatabaseRoundTrip()
    {
        var result = CreateController().GetLiveness();

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
