using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using HealthcareAPI.Controllers;

namespace HealthcareAPI.Tests.Controllers;

public class AiControllerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public string? ForwardedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                ForwardedBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private static (AiController Controller, StubHandler Handler) CreateController(
        HttpStatusCode status,
        string body)
    {
        var handler = new StubHandler(status, body);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://ai-service") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(instance => instance.CreateClient("AiService")).Returns(client);
        return (new AiController(factory.Object, NullLogger<AiController>.Instance), handler);
    }

    private static IFormFile PdfFile()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\nsynthetic");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "form.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    [Fact]
    public async Task ExtractIntake_ForwardsPdfAndReturnsAiResponse()
    {
        var (controller, _) = CreateController(
            HttpStatusCode.OK,
            "{\"isIntakeForm\":true,\"confidence\":0.9,\"fields\":{}}");

        var result = await controller.ExtractIntake(PdfFile(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Contains("isIntakeForm", objectResult.Value?.ToString());
    }

    [Fact]
    public async Task ExtractIntake_RejectsNonPdf()
    {
        var (controller, _) = CreateController(HttpStatusCode.OK, "{}");
        var bytes = Encoding.ASCII.GetBytes("not a pdf");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "form.txt");

        var result = await controller.ExtractIntake(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExtractIntake_Returns503WhenAiServiceFails()
    {
        var (controller, _) = CreateController(HttpStatusCode.ServiceUnavailable, "{}");

        var result = await controller.ExtractIntake(PdfFile(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
    }
}
