using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;
using HealthcareAPI.Controllers;

namespace HealthcareAPI.Tests.Controllers;

public class DoctorExaminationRoomsControllerTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<DoctorExaminationRoomsController>> _mockLogger;
    private readonly DoctorExaminationRoomsController _controller;

    public DoctorExaminationRoomsControllerTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<DoctorExaminationRoomsController>>();
        _controller = new DoctorExaminationRoomsController(_mockHttpClientFactory.Object, _mockLogger.Object);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetDoctorRooms_ReturnsOk_WithRoomsList()
    {
        // Arrange
        var doctorId = 1;
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"id\": 1, \"roomNumber\": \"101\"}]")
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains($"doctor/{doctorId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        _mockHttpClientFactory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(mockHandler.Object));

        // Act
        var result = await _controller.GetDoctorRooms(doctorId);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task GetDoctorRooms_ReturnsEmpty_WhenNoDoctorRooms()
    {
        // Arrange
        var doctorId = 999;
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        _mockHttpClientFactory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(mockHandler.Object));

        // Act
        var result = await _controller.GetDoctorRooms(doctorId);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task GetDoctorRooms_ReturnsError_WhenDatabaseApiError()
    {
        // Arrange
        var doctorId = 1;
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Database error")
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        _mockHttpClientFactory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(mockHandler.Object));

        // Act
        var result = await _controller.GetDoctorRooms(doctorId);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task AssignRoom_ReturnsOk_WithValidData()
    {
        // Arrange
        var request = new AssignExaminationRoomRequest { DoctorId = 1, ExaminationRoomId = 1 };
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1, \"doctorId\": 1}")
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        _mockHttpClientFactory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(mockHandler.Object));

        // Act
        var result = await _controller.AssignRoom(request);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task AssignRoom_ReturnsBadRequest_WhenInvalidData()
    {
        // Arrange
        var request = new AssignExaminationRoomRequest { DoctorId = -1, ExaminationRoomId = -1 };
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Invalid data")
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        _mockHttpClientFactory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(mockHandler.Object));

        // Act
        var result = await _controller.AssignRoom(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task RemoveAssignment_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var assignmentId = 1;
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\": \"removed\"}")
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        _mockHttpClientFactory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(mockHandler.Object));

        // Act
        var result = await _controller.RemoveAssignment(assignmentId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task RemoveAssignment_ReturnsError_WhenAssignmentNotFound()
    {
        // Arrange
        var assignmentId = 999;
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not found")
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        _mockHttpClientFactory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(mockHandler.Object));

        // Act
        var result = await _controller.RemoveAssignment(assignmentId);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, statusCodeResult.StatusCode);
    }
}

public class AssignExaminationRoomRequest
{
    public int DoctorId { get; set; }
    public int ExaminationRoomId { get; set; }
}
