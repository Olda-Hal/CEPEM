using Serilog.Context;

namespace HealthcareAPI.Middleware;

/// <summary>
/// Propagates an incoming X-Correlation-ID (or mints one) and pushes it into
/// the Serilog log context so every log entry for the request carries it.
/// Downstream calls get the same id via <see cref="HealthcareAPI.Handlers.AuthenticationHandler"/>.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
