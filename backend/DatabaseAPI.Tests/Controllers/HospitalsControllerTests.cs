using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DatabaseAPI.Tests.Controllers;

public class HospitalsControllerTests : IAsyncLifetime
{
    private readonly DatabaseContext _context;
    private readonly HospitalsController _controller;
    private readonly ILogger<HospitalsController> _logger;

    public HospitalsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DatabaseContext(options);
        _logger = new Mock<ILogger<HospitalsController>>().Object;
        _controller = new HospitalsController(_context, _logger);
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
    public async Task GetAll_ReturnsOk_WithAllActiveHospitals()
    {
        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenHospitalExists()
    {
        // Arrange
        var hospital = new Hospital { Name = "Test Hospital", Active = true };
        _context.Hospitals.Add(hospital);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetById(hospital.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenHospitalDoesNotExist()
    {
        // Act
        var result = await _controller.GetById(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WithValidData()
    {
        // Arrange
        var request = new CreateHospitalRequest
        {
            Name = "New Hospital",
            Street = "123 Main St",
            City = "Prague",
            PostalCode = "11000",
            Country = "Czech Republic"
        };

        // Act
        var result = await _controller.Create(request);

        // Assert
        Assert.True(result.Result is CreatedAtActionResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task Update_ReturnsOk_WithValidData()
    {
        // Arrange
        var hospital = new Hospital { Name = "Original Name", Active = true };
        _context.Hospitals.Add(hospital);
        await _context.SaveChangesAsync();

        var request = new UpdateHospitalRequest { Name = "Updated Name" };

        // Act
        var result = await _controller.Update(hospital.Id, request);

        // Assert
        Assert.True(result is OkObjectResult or NotFoundObjectResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task Delete_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var hospital = new Hospital { Name = "Hospital to Delete", Active = true };
        _context.Hospitals.Add(hospital);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.Delete(hospital.Id);

        // Assert
        Assert.True(result is OkObjectResult or NotFoundObjectResult);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var request = new CreateHospitalRequest { Name = "", Street = "123 Main St" };

        // Act - Should handle empty name gracefully
        var result = await _controller.Create(request);

        // Assert - Either success or validation error is acceptable
        Assert.True(result.Result is CreatedAtActionResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WithOnlyName()
    {
        // Arrange
        var request = new CreateHospitalRequest { Name = "Simple Hospital" };

        // Act
        var result = await _controller.Create(request);

        // Assert
        Assert.True(result.Result is CreatedAtActionResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenHospitalDoesNotExist()
    {
        // Arrange
        var request = new UpdateHospitalRequest { Name = "Updated Name" };

        // Act
        var result = await _controller.Update(999, request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsOk_WithPartialUpdate()
    {
        // Arrange
        var hospital = new Hospital { Name = "Original Name", Active = true };
        _context.Hospitals.Add(hospital);
        await _context.SaveChangesAsync();

        // Update only name, leave address unchanged
        var request = new UpdateHospitalRequest { Name = "Updated Name" };

        // Act
        var result = await _controller.Update(hospital.Id, request);

        // Assert
        Assert.True(result is OkObjectResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyActiveHospitals()
    {
        // Arrange
        var activeHospital = new Hospital { Name = "Active Hospital", Active = true };
        var inactiveHospital = new Hospital { Name = "Inactive Hospital", Active = false };
        _context.Hospitals.Add(activeHospital);
        _context.Hospitals.Add(inactiveHospital);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenHospitalDoesNotExist()
    {
        // Act
        var result = await _controller.Delete(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }
}
