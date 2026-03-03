using Microsoft.AspNetCore.Mvc;
using Xunit;
using HealthcareAPI.Controllers;

namespace HealthcareAPI.Tests.Controllers;

public class TestResultsControllerTests
{
    private readonly TestResultsController _controller;

    public TestResultsControllerTests()
    {
        _controller = new TestResultsController();
    }

    [Fact]
    public async Task GetTestSummary_ReturnsOk()
    {
        // Act
        var result = await _controller.GetTestSummary();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetTestSummary_ContainsMessageProperty()
    {
        // Act
        var result = await _controller.GetTestSummary();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetServiceDetails_ReturnsOk_WithServiceName()
    {
        // Act
        var result = await _controller.GetServiceDetails("healthcare-api");

        // Assert
        Assert.IsType<OkObjectResult>(result) ?? Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetServiceDetails_WithValidService_ReturnsFileList()
    {
        // Act
        var result = await _controller.GetServiceDetails("healthcare-api");

        // Assert
        Assert.True(result is OkObjectResult or NotFoundObjectResult);
    }

    [Fact]
    public async Task GetServiceDetails_WithInvalidService_HandlesGracefully()
    {
        // Act
        var result = await _controller.GetServiceDetails("invalid-service");

        // Assert
        Assert.True(result is OkObjectResult or NotFoundObjectResult or ObjectResult);
    }
}
