namespace DatabaseAPI.Middleware;

/// <summary>
/// Requires a shared service key on every request to DatabaseAPI.
/// Only HealthcareAPI (which derives X-Actor-* headers from validated JWT
/// claims) should ever call this service; the key prevents anything else
/// that reaches the container network from impersonating a caller.
/// </summary>
public class ServiceAuthMiddleware
{
    private const string ServiceKeyHeader = "X-Service-Key";

    private static readonly string[] ExemptPaths =
    {
        "/health",
        "/api/health"
    };

    private readonly RequestDelegate _next;

    public ServiceAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        if (ExemptPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var expectedKey = configuration["ServiceAuth:Key"]
            ?? Environment.GetEnvironmentVariable("SERVICE_AUTH_KEY");
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            // Fail closed: no key configured means nobody can call the API
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { error = "Service auth is not configured" });
            return;
        }

        var providedKey = context.Request.Headers[ServiceKeyHeader].FirstOrDefault();
        if (string.IsNullOrEmpty(providedKey) || !CryptographicFixedTimeEquals(providedKey, expectedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized service call" });
            return;
        }

        await _next(context);
    }

    private static bool CryptographicFixedTimeEquals(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
