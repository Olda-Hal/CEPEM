using Microsoft.AspNetCore.Mvc;
using Xunit;
using DatabaseAPI.Controllers;
using DatabaseAPI.Models;

namespace DatabaseAPI.Tests.Controllers;

public class HealthControllerTests
{
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _controller = new HealthController();
    }

    [Fact]
    public void GetHealth_ReturnsOkResult()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        Assert.IsType<ActionResult<HealthResponse>>(result);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void GetHealth_ReturnsCorrectHealthData()
    {
        // Act
        var result = _controller.GetHealth();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var healthResponse = Assert.IsType<HealthResponse>(okResult.Value);

        // Assert
        Assert.Equal("Healthy", healthResponse.Status);
        Assert.Equal("Database API", healthResponse.Service);
        Assert.Equal("1.0.0", healthResponse.Version);
    }

    [Fact]
    public void GetHealth_TimestampIsRecent()
    {
        // Arrange
        var beforeTest = DateTime.UtcNow;

        // Act
        var result = _controller.GetHealth();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var healthResponse = Assert.IsType<HealthResponse>(okResult.Value);

        // Assert
        var afterTest = DateTime.UtcNow;
        Assert.True(healthResponse.Timestamp >= beforeTest);
        Assert.True(healthResponse.Timestamp <= afterTest);
    }
}
