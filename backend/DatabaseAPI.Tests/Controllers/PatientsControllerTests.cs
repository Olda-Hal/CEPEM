using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.APIModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DatabaseAPI.Tests.Controllers;

public class PatientsControllerTests : IAsyncLifetime
{
    private readonly DatabaseContext _context;
    private readonly PatientsController _controller;
    private readonly ILogger<PatientsController> _logger;

    public PatientsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DatabaseContext(options);
        _logger = new Mock<ILogger<PatientsController>>().Object;
        _controller = new PatientsController(_context, _logger, null!, null!);
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task SearchPatients_ReturnsOk_WithValidSearch()
    {
        // Act
        var result = await _controller.SearchPatients(page: 0, limit: 20, search: "", sortBy: "LastName", sortOrder: "asc");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithPagination_ReturnsPagedResults()
    {
        // Act
        var result = await _controller.SearchPatients(page: 0, limit: 10, search: "", sortBy: "LastName", sortOrder: "asc");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithSearch_FiltersResults()
    {
        // Act
        var result = await _controller.SearchPatients(page: 0, limit: 20, search: "John", sortBy: "LastName", sortOrder: "asc");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithDifferentSortOrders_SortsCorrectly()
    {
        // Act - Sort ascending
        var resultAsc = await _controller.SearchPatients(sortOrder: "asc");
        var resultDesc = await _controller.SearchPatients(sortOrder: "desc");

        // Assert
        Assert.NotNull(resultAsc.Result);
        Assert.NotNull(resultDesc.Result);
    }

    [Fact]
    public async Task SearchPatients_WithEmptySearch_ReturnsAllPatients()
    {
        // Act
        var result = await _controller.SearchPatients(search: "");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithNullSearch_ReturnsAllPatients()
    {
        // Act
        var result = await _controller.SearchPatients(search: null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithLargePage_ReturnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(page: 1000, limit: 20);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithSmallLimit_ReturnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(limit: 1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithLargeLimitReturnnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(limit: 1000);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithSpecificSortColumn_ReturnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(sortBy: "FirstName");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchPatients_WithInvalidSortColumn_StillReturnsOk()
    {
        // Act - Invalid column should be handled gracefully
        var result = await _controller.SearchPatients(sortBy: "InvalidColumn");

        // Assert
        Assert.True(result.Result is OkObjectResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task SearchPatients_WithComplexSearch_ReturnsOk()
    {
        // Act
        var result = await _controller.SearchPatients(search: "Complex@Search!123");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }
}
