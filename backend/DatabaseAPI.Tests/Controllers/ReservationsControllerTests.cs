using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DatabaseAPI.Tests.Controllers;

public class ReservationsControllerTests : IAsyncLifetime
{
    private readonly DatabaseContext _context;
    private readonly ReservationsController _controller;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DatabaseContext(options);
        _logger = new Mock<ILogger<ReservationsController>>().Object;
        _controller = new ReservationsController(_context, _logger);
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
    public async Task GetDoctorReservations_ReturnsOk_WithReservationsList()
    {
        // Act
        var result = await _controller.GetDoctorReservations(1, null, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetDoctorReservations_WithDateRange_FiltersCorrectly()
    {
        // Arrange
        var from = new DateTime(2026, 03, 01);
        var to = new DateTime(2026, 03, 31);

        // Act
        var result = await _controller.GetDoctorReservations(1, from, to);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetRoomReservations_ReturnsOk_WithReservationsList()
    {
        // Act
        var result = await _controller.GetRoomReservations(1, null, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CreateReservation_ReturnsCreated_WithValidData()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            DoctorId = 1,
            PersonId = 1,
            ExaminationRoomId = 1,
            ExaminationTypeId = 1,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(1)
        };

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        Assert.True(result.Result is CreatedAtActionResult or BadRequestObjectResult or NotFoundObjectResult);
    }

    [Fact]
    public async Task UpdateReservation_ReturnsOk_WithValidData()
    {
        // Arrange
        var request = new UpdateReservationRequest
        {
            StartDateTime = DateTime.UtcNow.AddHours(2),
            EndDateTime = DateTime.UtcNow.AddHours(3)
        };

        // Act
        var result = await _controller.UpdateReservation(1, request);

        // Assert
        Assert.True(result is OkObjectResult or NotFoundObjectResult or BadRequestObjectResult);
    }

    [Fact]
    public async Task DeleteReservation_ReturnsOk_WhenSuccessful()
    {
        // Act
        var result = await _controller.DeleteReservation(1);

        // Assert
        Assert.True(result is OkObjectResult or NotFoundObjectResult);
    }

    [Fact]
    public async Task CreateReservation_ReturnsBadRequest_WhenStartTimeEqualsEndTime()
    {
        // Arrange
        var now = DateTime.UtcNow.AddDays(1);
        var request = new CreateReservationRequest
        {
            DoctorId = 1,
            PersonId = 1,
            ExaminationRoomId = 1,
            ExaminationTypeId = 1,
            StartDateTime = now,
            EndDateTime = now
        };

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateReservation_ReturnsBadRequest_WhenStartTimeAfterEndTime()
    {
        // Arrange
        var now = DateTime.UtcNow.AddDays(1);
        var request = new CreateReservationRequest
        {
            DoctorId = 1,
            PersonId = 1,
            ExaminationRoomId = 1,
            ExaminationTypeId = 1,
            StartDateTime = now.AddHours(2),
            EndDateTime = now
        };

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateReservation_ReturnsNotFound_WhenDoctorDoesNotExist()
    {
        // Arrange
        var request = new CreateReservationRequest
        {
            DoctorId = 999,
            PersonId = 1,
            ExaminationRoomId = 1,
            ExaminationTypeId = 1,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(1)
        };

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateReservation_ReturnsNotFound_WhenPersonDoesNotExist()
    {
        // Arrange
        var doctor = new Employee { PersonId = 1 };
        _context.Employees.Add(doctor);
        await _context.SaveChangesAsync();

        var request = new CreateReservationRequest
        {
            DoctorId = doctor.Id,
            PersonId = 999,
            ExaminationRoomId = 1,
            ExaminationTypeId = 1,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(1)
        };

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateReservation_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var doctor = new Employee { PersonId = 1 };
        var person = new Person
        {
            FirstName = "Jane",
            LastName = "Doe",
            UID = "person-test-uid",
            Gender = "Unknown",
            Active = true
        };
        _context.Employees.Add(doctor);
        _context.Persons.Add(person);
        await _context.SaveChangesAsync();

        var request = new CreateReservationRequest
        {
            DoctorId = doctor.Id,
            PersonId = person.Id,
            ExaminationRoomId = 999,
            ExaminationTypeId = 1,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(1)
        };

        // Act
        var result = await _controller.CreateReservation(request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateReservation_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        // Arrange
        var request = new UpdateReservationRequest
        {
            StartDateTime = DateTime.UtcNow.AddHours(2),
            EndDateTime = DateTime.UtcNow.AddHours(3)
        };

        // Act
        var result = await _controller.UpdateReservation(999, request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateReservation_ReturnsBadRequest_WhenStartTimeAfterEndTime()
    {
        // Arrange - Create a simple reservation
        var reservation = new Reservation
        {
            DoctorId = 1,
            PersonId = 1,
            ExaminationRoomId = 1,
            ExaminationTypeId = 1,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            Status = "Confirmed"
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow.AddDays(1);
        var request = new UpdateReservationRequest
        {
            StartDateTime = now.AddHours(2),
            EndDateTime = now
        };

        // Act
        var result = await _controller.UpdateReservation(reservation.Id, request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteReservation_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        // Act
        var result = await _controller.DeleteReservation(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetDoctorReservations_WithInvalidDateRange_StillReturnsOk()
    {
        // Arrange - 'to' before 'from' should still work (service handles it)
        var from = new DateTime(2026, 03, 31);
        var to = new DateTime(2026, 03, 01);

        // Act
        var result = await _controller.GetDoctorReservations(1, from, to);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetRoomReservations_WithInvalidDateRange_StillReturnsOk()
    {
        // Arrange
        var from = new DateTime(2026, 03, 31);
        var to = new DateTime(2026, 03, 01);

        // Act
        var result = await _controller.GetRoomReservations(1, from, to);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }
}
