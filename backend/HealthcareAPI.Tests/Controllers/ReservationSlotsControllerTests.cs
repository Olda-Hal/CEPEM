using HealthcareAPI.Controllers;
using HealthcareAPI.Middleware;
using HealthcareAPI.Models;
using HealthcareAPI.Services;
using HealthcareAPI.Tests.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Security.Claims;
using Xunit;

namespace HealthcareAPI.Tests.Controllers;

/// <summary>
/// ConfirmSlot is the single slot endpoint HealthcareAPI owns (everything
/// else goes through YARP): it must default the doctor to the authenticated
/// employee and faithfully proxy DatabaseAPI responses.
/// </summary>
public class ReservationSlotsControllerTests
{
    private readonly StubHttpMessageHandler _handler = new();
    private readonly ReservationSlotsController _controller;

    public ReservationSlotsControllerTests()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(_handler) { BaseAddress = new Uri("http://database-api") });

        _controller = new ReservationSlotsController(factory.Object, NullLogger<ReservationSlotsController>.Instance);
    }

    private void AuthenticateAs(int employeeId)
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, employeeId.ToString())
                }, "TestAuthentication"))
            }
        };
    }

    [Fact]
    public async Task ConfirmSlot_WithoutDoctorId_UsesAuthenticatedEmployee()
    {
        AuthenticateAs(42);
        _handler.EnqueueJson(HttpStatusCode.OK, "{\"id\": 5, \"status\": \"RESERVED\"}");

        var result = await _controller.ConfirmSlot(5, null);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Contains("RESERVED", content.Content);
        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, _handler.Requests[0].Method);
        Assert.Contains("/api/reservations/slots/5/confirm", _handler.Requests[0].Url);
    }

    [Fact]
    public async Task ConfirmSlot_WithDoctorId_ForwardsIt()
    {
        AuthenticateAs(42);
        _handler.EnqueueJson(HttpStatusCode.OK, "{\"id\": 5}");

        var result = await _controller.ConfirmSlot(5, new ConfirmReservationSlotRequest { DoctorId = 7 });

        Assert.IsType<ContentResult>(result);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task ConfirmSlot_WithoutDoctorIdAndWithoutClaim_ReturnsBadRequest()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "TestAuthentication"))
            }
        };

        var result = await _controller.ConfirmSlot(5, null);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task ConfirmSlot_PropagatesDatabaseApiErrorStatus()
    {
        AuthenticateAs(42);
        _handler.EnqueueJson(HttpStatusCode.BadRequest, "{\"error\": \"only blocked slots can be confirmed\"}");

        var result = await _controller.ConfirmSlot(5, new ConfirmReservationSlotRequest { DoctorId = 42 });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, statusResult.StatusCode);
    }

    [Fact]
    public async Task ConfirmSlot_OnDatabaseApiException_Returns500()
    {
        AuthenticateAs(42);
        _handler.EnqueueException(new HttpRequestException("down"));

        var result = await _controller.ConfirmSlot(5, new ConfirmReservationSlotRequest { DoctorId = 42 });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}

/// <summary>
/// Activity logging middleware: skips health/log endpoints, records the
/// request with status code even when the pipeline throws.
/// </summary>
public class ActivityLoggingMiddlewareTests
{
    private sealed class RecordingLogService : IActivityLogService
    {
        public readonly List<ActivityLog> Logs = new();

        public Task LogActivityAsync(ActivityLog log)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }
    }

    private static ClaimsPrincipal Employee(int id, string email)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Email, email)
        }, "TestAuthentication"));
    }

    [Fact]
    public async Task Invoke_LogsRequest_WithStatusCodeAndActor()
    {
        var logs = new RecordingLogService();
        var middleware = new ActivityLoggingMiddleware(async ctx =>
        {
            ctx.Response.StatusCode = 201;
            await Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/patients";
        context.Request.Method = "POST";
        context.User = Employee(3, "user@example.com");

        await middleware.InvokeAsync(context, logs);

        var log = Assert.Single(logs.Logs);
        Assert.Equal("/api/patients", log.Action);
        Assert.Equal("POST", log.Method);
        Assert.Equal(3, log.EmployeeId);
        Assert.Equal("user@example.com", log.EmployeeEmail);
        Assert.Equal(201, log.StatusCode);
        Assert.Equal("HealthcareAPI", log.Service);
    }

    [Fact]
    public async Task Invoke_LogsEvenWhenPipelineThrows()
    {
        var logs = new RecordingLogService();
        var middleware = new ActivityLoggingMiddleware(_ => throw new InvalidOperationException("boom"));

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/patients";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, logs));

        Assert.Single(logs.Logs);
    }

    [Theory]
    [InlineData("/api/activitylogs")]
    [InlineData("/health")]
    [InlineData("/swagger/index.html")]
    public async Task Invoke_SkipsHealthLogAndSwaggerPaths(string path)
    {
        var logs = new RecordingLogService();
        var nextCalled = false;
        var middleware = new ActivityLoggingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context, logs);

        Assert.True(nextCalled);
        Assert.Empty(logs.Logs);
    }
}
