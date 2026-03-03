using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;
using HealthcareAPI.Controllers;
using HealthcareAPI.Models;
using HealthcareAPI.Services;

namespace HealthcareAPI.Tests.Controllers;

public class EmployeesControllerTests
{
    private readonly Mock<IEmployeeService> _mockEmployeeService;
    private readonly EmployeesController _controller;

    public EmployeesControllerTests()
    {
        _mockEmployeeService = new Mock<IEmployeeService>();
        _controller = new EmployeesController(_mockEmployeeService.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetCurrentEmployee_ReturnsOk_WhenEmployeeExists()
    {
        // Arrange
        var employee = new Employee
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };

        _mockEmployeeService.Setup(s => s.GetCurrentEmployeeAsync(1))
            .ReturnsAsync(employee);

        // Act
        var result = await _controller.GetCurrentEmployee();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedEmployee = Assert.IsType<Employee>(okResult.Value);
        Assert.Equal(employee.Id, returnedEmployee.Id);
    }

    [Fact]
    public async Task GetCurrentEmployee_ReturnsNotFound_WhenEmployeeDoesNotExist()
    {
        // Arrange
        _mockEmployeeService.Setup(s => s.GetCurrentEmployeeAsync(1))
            .ReturnsAsync((Employee?)null);

        // Act
        var result = await _controller.GetCurrentEmployee();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetDashboardStats_ReturnsOk_WithStats()
    {
        // Arrange
        var stats = new { totalPatients = 50, reservations = 10, systemStatus = "Active" };

        _mockEmployeeService.Setup(s => s.GetDashboardStatsAsync(1))
            .ReturnsAsync(stats as object);

        // Act
        var result = await _controller.GetDashboardStats();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetDashboardStats_ReturnsNotFound_WhenStatsDoNotExist()
    {
        // Arrange
        _mockEmployeeService.Setup(s => s.GetDashboardStatsAsync(1))
            .ReturnsAsync((object?)null);

        // Act
        var result = await _controller.GetDashboardStats();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
