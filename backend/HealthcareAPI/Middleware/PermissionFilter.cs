using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;

namespace HealthcareAPI.Middleware;

public class PermissionFilter : IAsyncActionFilter
{
    private readonly string _permissionCode;
    private readonly string? _scopeType;
    private readonly string? _scopeParamName;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PermissionFilter> _logger;

    public PermissionFilter(string permissionCode, string? scopeType, string? scopeParamName, IHttpClientFactory httpClientFactory, ILogger<PermissionFilter> logger)
    {
        _permissionCode = permissionCode;
        _scopeType = scopeType;
        _scopeParamName = scopeParamName;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user?.Identity == null || !user.Identity.IsAuthenticated)
        {
            await WriteError(context, 401, "Unauthorized");
            return;
        }

        var employeeIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (employeeIdClaim == null || !int.TryParse(employeeIdClaim.Value, out var employeeId))
        {
            await WriteError(context, 403, "Forbidden", "Missing or invalid employee identity");
            return;
        }

        int? scopeId = null;
        if (!string.IsNullOrEmpty(_scopeParamName))
        {
            // Try route values first
            if (context.RouteData.Values.TryGetValue(_scopeParamName, out var v) && v != null && int.TryParse(v.ToString(), out var r))
            {
                scopeId = r;
            }
            else if (context.HttpContext.Request.Query.TryGetValue(_scopeParamName, out var qv) && int.TryParse(qv.FirstOrDefault(), out var q))
            {
                scopeId = q;
            }
        }

        var query = $"?employeeId={employeeId}&permissionCode={Uri.EscapeDataString(_permissionCode)}";
        if (!string.IsNullOrEmpty(_scopeType)) query += $"&scopeType={Uri.EscapeDataString(_scopeType)}";
        if (scopeId.HasValue) query += $"&scopeId={scopeId.Value}";

        var client = _httpClientFactory.CreateClient("DatabaseAPI");
        try
        {
            var resp = await client.GetAsync($"/api/admin/permissions/effective{query}", context.HttpContext.RequestAborted);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Permission evaluation failed for permission {PermissionCode}, employee {EmployeeId}: status {StatusCode}",
                    _permissionCode, employeeId, resp.StatusCode);
                await WriteError(context, 403, "Forbidden", "Permission evaluation failed");
                return;
            }

            var content = await resp.Content.ReadAsStringAsync(context.HttpContext.RequestAborted);
            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("allowed", out var allowed) && allowed.GetBoolean())
            {
                await next();
                return;
            }

            string? reason = null;
            if (doc.RootElement.TryGetProperty("reason", out var reasonElement) && reasonElement.ValueKind == JsonValueKind.String)
            {
                reason = reasonElement.GetString();
            }
            await WriteError(context, 403, "Forbidden", reason ?? "Access denied", _permissionCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating permission {PermissionCode} for employee {EmployeeId}", _permissionCode, employeeId);
            await WriteError(context, 500, "Internal server error", "Permission evaluation error");
        }
    }

    private static async Task WriteError(ActionExecutingContext context, int statusCode, string error, string? reason = null, string? permission = null)
    {
        context.HttpContext.Response.StatusCode = statusCode;
        context.HttpContext.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { error, reason, permission });
        await context.HttpContext.Response.WriteAsync(payload);
    }
}
