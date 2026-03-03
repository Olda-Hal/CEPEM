using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;
using HealthcareAPI.Controllers;

namespace HealthcareAPI.Tests.Controllers;

public class ExaminationRoomsControllerTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<ExaminationRoomsController>> _mockLogger;
    private readonly ExaminationRoomsController _controller;

    public ExaminationRoomsControllerTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<ExaminationRoomsController>>();
        _controller = new ExaminationRoomsController(_mockHttpClientFactory.Object, _mockLogger.Object);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetHospitalRooms_ReturnsOk_WithRoomsList()
    {
        // Arrange
        var hospitalId = 1;
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"id\": 1, \"name\": \"Room 101\"}]")
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains($"hospital/{hospitalId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        _mockHttpClientFactory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(mockHandler.Object));

        // Act
        var result = await _controller.GetHospitalRooms(hospitalId);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task GetHospitalRooms_ReturnsEmpty_WhenNoRooms()
    {
        // Arrange
        var hospitalId = 999;
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
        var result = await _controller.GetHospitalRooms(hospitalId);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task CreateRoom_ReturnsOk_WithValidData()
    {
        // Arrange
        var request = new CreateExaminationRoomRequest { Name = "New Room", HospitalId = 1 };
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1, \"name\": \"New Room\"}")
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
        var result = await _controller.CreateRoom(request);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task CreateRoom_ReturnsBadRequest_WhenMissingFields()
    {
        // Arrange
        var request = new CreateExaminationRoomRequest { Name = "", HospitalId = 0 };
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
        var result = await _controller.CreateRoom(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task UpdateRoom_ReturnsOk_WithValidData()
    {
        // Arrange
        var roomId = 1;
        var request = new UpdateExaminationRoomRequest { Name = "Updated Room" };
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1, \"name\": \"Updated Room\"}")
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
        var result = await _controller.UpdateRoom(roomId, request);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task DeleteRoom_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var roomId = 1;
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\": \"deleted\"}")
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
        var result = await _controller.DeleteRoom(roomId);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task DeleteRoom_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        // Arrange
        var roomId = 999;
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
        var result = await _controller.DeleteRoom(roomId);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, statusCodeResult.StatusCode);
    }
}

public class CreateExaminationRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public int HospitalId { get; set; }
}

public class UpdateExaminationRoomRequest
{
    public string? Name { get; set; }
}
