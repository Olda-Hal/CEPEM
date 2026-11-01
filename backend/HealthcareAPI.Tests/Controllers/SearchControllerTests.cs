using System.Net;
using System.Security.Claims;
using HealthcareAPI.Authorization;
using HealthcareAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HealthcareAPI.Tests.Controllers;

/// <summary>
/// The gateway only asks DatabaseAPI for categories the caller may see.
/// </summary>
public class SearchControllerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"query\":\"eva\",\"patients\":[],\"visits\":[],\"documents\":[],\"evaluations\":[],\"employees\":[]}",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
        }
    }

    private static SearchController CreateController(
        CapturingHandler handler,
        bool patients,
        bool evaluations,
        bool employees)
    {
        var evaluator = new Mock<IPermissionEvaluator>();
        evaluator
            .Setup(service => service.IsAllowedAsync(1, "patients.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(patients);
        evaluator
            .Setup(service => service.IsAllowedAsync(1, "evaluations.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluations);
        evaluator
            .Setup(service => service.IsAllowedAsync(1, "employees.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(employees);

        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(service => service.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("http://database-api") });

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "1") },
            "Test");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        return new SearchController(
            evaluator.Object,
            factory.Object,
            NullLogger<SearchController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Fact]
    public async Task Search_PassesOnlyPermittedCategoriesToDatabaseApi()
    {
        var handler = new CapturingHandler();
        var controller = CreateController(handler, patients: true, evaluations: false, employees: true);

        var result = await controller.Search("eva", 5);

        Assert.IsType<ContentResult>(result);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("q=eva", handler.LastRequestUri);
        Assert.Contains("patients=true", handler.LastRequestUri);
        Assert.Contains("evaluations=false", handler.LastRequestUri);
        Assert.Contains("employees=true", handler.LastRequestUri);
    }

    [Fact]
    public async Task Search_WithoutAnyPermission_DoesNotCallDatabaseApi()
    {
        var handler = new CapturingHandler();
        var controller = CreateController(handler, patients: false, evaluations: false, employees: false);

        var result = await controller.Search("eva", 5);

        Assert.IsType<ContentResult>(result);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task Search_WithShortQuery_ReturnsEmptyWithoutCallingDatabaseApi()
    {
        var handler = new CapturingHandler();
        var controller = CreateController(handler, patients: true, evaluations: true, employees: true);

        var result = await controller.Search("e", 5);

        Assert.IsType<ContentResult>(result);
        Assert.Null(handler.LastRequestUri);
    }
}
