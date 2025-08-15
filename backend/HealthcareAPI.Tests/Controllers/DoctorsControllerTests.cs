using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;
using HealthcareAPI.Controllers;
using HealthcareAPI.Models;
using HealthcareAPI.Services;

namespace HealthcareAPI.Tests.Controllers;

public class DoctorsControllerTests
{
    private readonly Mock<IDoctorService> _mockDoctorService;
    private readonly DoctorsController _controller;

    public DoctorsControllerTests()
    {
        _mockDoctorService = new Mock<IDoctorService>();
        _controller = new DoctorsController(_mockDoctorService.Object);
        
        // Setup ClaimsPrincipal for testing
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
    public async Task GetCurrentDoctor_ReturnsOk_WhenDoctorExists()
    {
        // Arrange
        var doctor = new Doctor
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Specialization = "Cardiology"
        };
        _mockDoctorService.Setup(s => s.GetCurrentDoctorAsync(1))
            .ReturnsAsync(doctor);

        // Act
        var result = await _controller.GetCurrentDoctor();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedDoctor = Assert.IsType<Doctor>(okResult.Value);
        Assert.Equal(doctor.Id, returnedDoctor.Id);
        Assert.Equal(doctor.FirstName, returnedDoctor.FirstName);
    }

    [Fact]
    public async Task GetCurrentDoctor_ReturnsNotFound_WhenDoctorDoesNotExist()
    {
        // Arrange
        _mockDoctorService.Setup(s => s.GetCurrentDoctorAsync(1))
            .ReturnsAsync((Doctor?)null);

        // Act
        var result = await _controller.GetCurrentDoctor();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetDashboardStats_ReturnsOk_WhenStatsExist()
    {
        // Arrange
        var stats = new { totalDoctors = 5, systemStatus = "Active" };
        _mockDoctorService.Setup(s => s.GetDashboardStatsAsync(1))
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
        _mockDoctorService.Setup(s => s.GetDashboardStatsAsync(1))
            .ReturnsAsync((object)null!);

        // Act
        var result = await _controller.GetDashboardStats();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
