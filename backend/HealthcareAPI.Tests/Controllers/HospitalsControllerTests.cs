using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;
using HealthcareAPI.Controllers;

namespace HealthcareAPI.Tests.Controllers;

public class HospitalsControllerTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<HospitalsController>> _mockLogger;
    private readonly HospitalsController _controller;

    public HospitalsControllerTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<HospitalsController>>();
        _controller = new HospitalsController(_mockHttpClientFactory.Object, _mockLogger.Object);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetAll_ReturnsOk_WithHospitals()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"id\": 1, \"name\": \"Hospital 1\"}]")
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
        var result = await _controller.GetAll();

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsError_WhenDatabaseApiError()
    {
        // Arrange
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);

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
        var result = await _controller.GetAll();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsOk_WithValidData()
    {
        // Arrange
        var request = new CreateHospitalRequest { Name = "New Hospital", Street = "Main St", City = "Prague" };
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1, \"name\": \"New Hospital\"}")
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
        var result = await _controller.Create(request);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenInvalidData()
    {
        // Arrange
        var request = new CreateHospitalRequest { Name = "" };
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
        var result = await _controller.Create(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsOk_WithValidData()
    {
        // Arrange
        var request = new UpdateHospitalRequest { Name = "Updated Hospital" };
        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": 1, \"name\": \"Updated Hospital\"}")
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
        var result = await _controller.Update(1, request);

        // Assert
        Assert.IsType<ContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsOk_WhenSuccessful()
    {
        // Arrange
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
        var result = await _controller.Delete(1);

        // Assert
        Assert.IsType<ContentResult>(result);
    }
}

public class CreateHospitalRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class UpdateHospitalRequest
{
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}
