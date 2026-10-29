using Microsoft.AspNetCore.Mvc;
using Xunit;
using HealthcareAPI.Controllers;

namespace HealthcareAPI.Tests.Controllers;

public class HealthControllerTests
{
    private readonly HealthController _controller;

    public HealthControllerTests()
    {
        _controller = new HealthController();
    }

    [Fact]
    public void GetHealth_ReturnsOk()
    {
        // Act
        var result = _controller.Get();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetHealth_ReturnsHealthyStatus()
    {
        // Act
        var result = _controller.Get();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("\"Status\":\"Healthy\"", json);
    }

    [Fact]
    public void GetHealth_ReturnsServiceName()
    {
        // Act
        var result = _controller.Get();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("\"Service\":\"HealthcareAPI\"", json);
    }

    [Fact]
    public void GetHealth_StatusCodeIsOk()
    {
        // Act
        var result = _controller.Get();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }
}
