using Moq;
using Xunit;
using HealthcareAPI.Models;
using HealthcareAPI.Repositories;
using HealthcareAPI.Services;

namespace HealthcareAPI.Tests.Services;

public class DoctorServiceTests
{
    private readonly Mock<IDoctorRepository> _mockRepository;
    private readonly DoctorService _service;

    public DoctorServiceTests()
    {
        _mockRepository = new Mock<IDoctorRepository>();
        _service = new DoctorService(_mockRepository.Object);
    }

    [Fact]
    public async Task GetCurrentDoctorAsync_ReturnsDoctor_WhenDoctorExists()
    {
        // Arrange
        var doctor = new Doctor
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(doctor);

        // Act
        var result = await _service.GetCurrentDoctorAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(doctor.Id, result.Id);
        Assert.Equal(doctor.FirstName, result.FirstName);
    }

    [Fact]
    public async Task GetCurrentDoctorAsync_ReturnsNull_WhenDoctorDoesNotExist()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync((Doctor?)null);

        // Act
        var result = await _service.GetCurrentDoctorAsync(1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsStats_WhenDoctorExists()
    {
        // Arrange
        var doctor = new Doctor
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(doctor);
        _mockRepository.Setup(r => r.GetTotalCountAsync())
            .ReturnsAsync(5);

        // Act
        var result = await _service.GetDashboardStatsAsync(1);

        // Assert
        Assert.NotNull(result);
    }
}
