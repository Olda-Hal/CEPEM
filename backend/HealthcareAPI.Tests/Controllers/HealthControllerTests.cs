using System.Net;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using HealthcareAPI.Controllers;

namespace HealthcareAPI.Tests.Controllers;

public class HealthControllerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public StubHandler(HttpStatusCode status)
        {
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status));
    }

    private static HealthController CreateController(HttpStatusCode status)
    {
        var client = new HttpClient(new StubHandler(status))
        {
            BaseAddress = new Uri("http://database-api")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(instance => instance.CreateClient("DatabaseAPI")).Returns(client);
        return new HealthController(factory.Object);
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWhenDatabaseApiIsHealthy()
    {
        var result = await CreateController(HttpStatusCode.OK).Get();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("\"Status\":\"Healthy\"", json);
        Assert.Contains("\"Service\":\"HealthcareAPI\"", json);
    }

    [Fact]
    public async Task GetHealth_Returns503WhenDatabaseApiIsDown()
    {
        var result = await CreateController(HttpStatusCode.ServiceUnavailable).Get();

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    [Fact]
    public void GetLiveness_AlwaysReturnsOk()
    {
        var result = CreateController(HttpStatusCode.ServiceUnavailable).GetLiveness();

        Assert.IsType<OkObjectResult>(result);
    }
}
