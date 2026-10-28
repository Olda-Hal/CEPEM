using HealthcareAPI.Authorization;
using HealthcareAPI.Middleware;
using HealthcareAPI.Tests.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Security.Claims;
using Xunit;

namespace HealthcareAPI.Tests.Authorization;

public class PermissionEvaluatorTests : IDisposable
{
    private readonly StubHttpMessageHandler _handler = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly PermissionEvaluator _evaluator;

    public PermissionEvaluatorTests()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(_handler) { BaseAddress = new Uri("http://database-api") });

        _evaluator = new PermissionEvaluator(factory.Object, _cache, NullLogger<PermissionEvaluator>.Instance);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task IsAllowed_ReturnsTrue_WhenDatabaseApiAllows()
    {
        var allowed = await _evaluator.IsAllowedAsync(7, "patients.read", CancellationToken.None);

        Assert.True(allowed);
        Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, _handler.Requests[0].Method);
        Assert.Contains("/api/admin/permissions/effective", _handler.Requests[0].Url);
        Assert.Contains("employeeId=7", _handler.Requests[0].Url);
        Assert.Contains("permissionCode=patients.read", _handler.Requests[0].Url);
    }

    [Fact]
    public async Task IsAllowed_ReturnsFalse_WhenDatabaseApiDenies()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, "{\"allowed\": false, \"reason\": \"role denies\"}");

        var allowed = await _evaluation_with_cache_bypass();

        Assert.False(allowed);
    }

    private Task<bool?> _evaluation_with_cache_bypass()
    {
        // distinct permission code => distinct cache key
        return _evaluator.IsAllowedAsync(7, "patients.read.deny-case", CancellationToken.None);
    }

    [Fact]
    public async Task IsAllowed_CachesResult_ForThirtySeconds()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, "{\"allowed\": true}");

        await _evaluator.IsAllowedAsync(8, "events.write", CancellationToken.None); // cached here
        _handler.EnqueueJson(HttpStatusCode.OK, "{\"allowed\": false}");
        var second = await _evaluator.IsAllowedAsync(8, "events.write", CancellationToken.None);

        Assert.True(second);
        Assert.Single(_handler.Requests); // the second call never hit the API
    }

    [Fact]
    public async Task IsAllowed_ReturnsNull_OnHttpFailure_AndDoesNotCache()
    {
        _handler.EnqueueJson(HttpStatusCode.ServiceUnavailable, "{}");

        var first = await _evaluator.IsAllowedAsync(9, "patients.read", CancellationToken.None);

        Assert.Null(first);

        _handler.EnqueueJson(HttpStatusCode.OK, "{\"allowed\": true}");
        var second = await _evaluator.IsAllowedAsync(9, "patients.read", CancellationToken.None);

        Assert.True(second); // a transient failure must not be cached
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task IsAllowed_ReturnsNull_OnException()
    {
        _handler.EnqueueException(new HttpRequestException("connection refused"));

        var allowed = await _evaluator.IsAllowedAsync(10, "audit.read", CancellationToken.None);

        Assert.Null(allowed);
    }

    [Fact]
    public async Task IsAllowed_ReturnsFalse_WhenBodyHasNoAllowedProperty()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, "{}");

        var allowed = await _evaluator.IsAllowedAsync(11, "settings.read", CancellationToken.None);

        Assert.False(allowed); // TryGetProperty misses -> empty default returns false
    }
}

public class PermissionAuthorizationHandlerTests
{
    private readonly Mock<IPermissionEvaluator> _evaluator = new();
    private readonly PermissionAuthorizationHandler _handler;

    public PermissionAuthorizationHandlerTests()
    {
        _handler = new PermissionAuthorizationHandler(_evaluator.Object);
    }

    private AuthorizationHandlerContext BuildContext(bool authenticated, string? employeeId)
    {
        var requirement = new PermissionRequirement("patients.read");
        var claims = employeeId != null
            ? new[] { new Claim(ClaimTypes.NameIdentifier, employeeId) }
            : Array.Empty<Claim>();
        var identity = authenticated
            ? new ClaimsIdentity(claims, "TestAuthentication")
            : new ClaimsIdentity(claims);
        var user = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = user };

        return new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);
    }

    [Fact]
    public async Task Handle_Succeeds_WhenEvaluatorAllows()
    {
        _evaluator.Setup(e => e.IsAllowedAsync(5, "patients.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var context = BuildContext(authenticated: true, employeeId: "5");

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_DoesNotSucceed_WhenEvaluatorDenies()
    {
        _evaluator.Setup(e => e.IsAllowedAsync(5, "patients.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var context = BuildContext(authenticated: true, employeeId: "5");

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_DoesNotSucceed_WhenEvaluationFails()
    {
        _evaluator.Setup(e => e.IsAllowedAsync(5, "patients.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool?)null);
        var context = BuildContext(authenticated: true, employeeId: "5");

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_DoesNotSucceed_WhenUnauthenticated()
    {
        var context = BuildContext(authenticated: false, employeeId: "5");

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        _evaluator.Verify(e => e.IsAllowedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotSucceed_WhenClaimMissing()
    {
        var context = BuildContext(authenticated: true, employeeId: null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}

public class PermissionFilterTests
{
    private readonly StubHttpMessageHandler _handler = new();

    private PermissionFilter BuildFilter(string code = "patients.read", string? scopeType = null, string? scopeParam = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("DatabaseAPI"))
            .Returns(new HttpClient(_handler) { BaseAddress = new Uri("http://database-api") });
        return new PermissionFilter(code, scopeType, scopeParam, factory.Object, NullLogger<PermissionFilter>.Instance);
    }

    private static ActionExecutingContext BuildContext(ClaimsPrincipal user, Dictionary<string, object?>? routeValues = null)
    {
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Response.Body = new MemoryStream(); // Response.Body is Stream.Null by default
        var routeData = new RouteData();
        if (routeValues != null)
        {
            foreach (var (key, value) in routeValues)
                routeData.Values[key] = value;
        }

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    private static int RepositoryCallCount(StubHttpMessageHandler handler) => handler.Requests.Count;

    private static ClaimsPrincipal AuthenticatedUser(string employeeId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, employeeId) }, "TestAuthentication"));

    [Fact]
    public async Task Filter_Writes401_WhenUnauthenticated()
    {
        var filter = BuildFilter();
        var context = BuildContext(new ClaimsPrincipal(new ClaimsIdentity()));
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled);
        Assert.Equal(401, context.HttpContext.Response.StatusCode);
        Assert.Equal(0, RepositoryCallCount(_handler));
    }

    [Fact]
    public async Task Filter_Writes403_WhenEmployeeClaimMissing()
    {
        var filter = BuildFilter();
        var context = BuildContext(new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "TestAuthentication")));

        await filter.OnActionExecutionAsync(context, () =>
            throw new InvalidOperationException("next must not run"));

        Assert.Equal(403, context.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Filter_CallsNext_WhenAllowed()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, "{\"allowed\": true}");
        var filter = BuildFilter();
        var context = BuildContext(AuthenticatedUser("42"));
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Filter_Writes403WithReason_WhenDenied()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, "{\"allowed\": false, \"reason\": \"role denies\"}");
        var filter = BuildFilter();
        var context = BuildContext(AuthenticatedUser("42"));

        await filter.OnActionExecutionAsync(context, () =>
            throw new InvalidOperationException("next must not run"));

        Assert.Equal(403, context.HttpContext.Response.StatusCode);
        context.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.HttpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("role denies", body);
        Assert.Contains("patients.read", body);
    }

    [Fact]
    public async Task Filter_Writes403_WhenEvaluationEndpointFails()
    {
        _handler.EnqueueJson(HttpStatusCode.InternalServerError, "{}");
        var filter = BuildFilter();
        var context = BuildContext(AuthenticatedUser("42"));

        await filter.OnActionExecutionAsync(context, () =>
            throw new InvalidOperationException("next must not run"));

        Assert.Equal(403, context.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Filter_Writes500_OnException()
    {
        _handler.EnqueueException(new HttpRequestException("boom"));
        var filter = BuildFilter();
        var context = BuildContext(AuthenticatedUser("42"));

        await filter.OnActionExecutionAsync(context, () =>
            throw new InvalidOperationException("next must not run"));

        Assert.Equal(500, context.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Filter_PassesScopeFromRouteValues()
    {
        _handler.EnqueueJson(HttpStatusCode.OK, "{\"allowed\": true}");
        var filter = BuildFilter("patients.read", scopeType: "Hospital", scopeParam: "hospitalId");
        var context = BuildContext(AuthenticatedUser("42"), new Dictionary<string, object?> { ["hospitalId"] = 7 });
        context.HttpContext.Response.Body = new MemoryStream();

        await filter.OnActionExecutionAsync(context, () =>
            Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object())));

        Assert.Single(_handler.Requests);
        Assert.Contains("scopeType=Hospital", _handler.Requests[0].Url);
        Assert.Contains("scopeId=7", _handler.Requests[0].Url);
    }

    private static void EnsureBodyStream(ActionExecutingContext context)
    {
        context.HttpContext.Response.Body = new MemoryStream();
    }

    [Fact]
    public async Task Filter_DefaultsResponseBodyWritable()
    {
        // guard helper so the tests above don't crash on missing streams
        _handler.EnqueueJson(HttpStatusCode.OK, "{\"allowed\": true}");
        var filter = BuildFilter();
        var context = BuildContext(AuthenticatedUser("42"));
        EnsureBodyStream(context);

        await filter.OnActionExecutionAsync(context, () =>
            Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object())));

        context.HttpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        // success path does not write anything
        Assert.Equal(200, context.HttpContext.Response.StatusCode);
    }
}
