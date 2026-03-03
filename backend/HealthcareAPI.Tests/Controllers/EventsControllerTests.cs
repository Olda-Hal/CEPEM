using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text.Json;
using Xunit;
using HealthcareAPI.Controllers;

namespace HealthcareAPI.Tests.Controllers;

public class EventsControllerTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly EventsController _controller;

    public EventsControllerTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _controller = new EventsController(_mockHttpClientFactory.Object);
    }

    [Fact]
    public async Task GetEventOptions_ReturnsOk_WithOptions()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"options\": []}")
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
        var result = await _controller.GetEventOptions();

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task GetEventOptions_ReturnsError_WhenDatabaseApiError()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Error")
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
        var result = await _controller.GetEventOptions();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_ReturnsOk_WithValidData()
    {
        // Arrange
        var eventData = JsonSerializer.SerializeToElement(new { name = "Event 1" });
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1, \"name\": \"Event 1\"}")
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
        var result = await _controller.CreateEvent(eventData);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task CreateEvent_ReturnsBadRequest_WhenInvalidData()
    {
        // Arrange
        var eventData = JsonSerializer.SerializeToElement(new { });
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Invalid event data")
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
        var result = await _controller.CreateEvent(eventData);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task CreateExaminationType_ReturnsOk_WithValidData()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { name = "Type 1" });
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1}")
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
        var result = await _controller.CreateExaminationType(data);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task CreateSymptom_ReturnsOk_WithValidData()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { name = "Symptom 1" });
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1}")
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
        var result = await _controller.CreateSymptom(data);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task CreateInjuryType_ReturnsOk_WithValidData()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { name = "Injury 1" });
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1}")
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
        var result = await _controller.CreateInjuryType(data);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task CreateVaccineType_ReturnsOk_WithValidData()
    {
        // Arrange
        var data = JsonSerializer.SerializeToElement(new { name = "Vaccine 1" });
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1}")
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
        var result = await _controller.CreateVaccineType(data);

        // Assert
        Assert.IsType<ContentResult>(result);
    }
}
