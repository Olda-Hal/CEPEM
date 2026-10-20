using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using System.Text.Json;

namespace HealthcareAPI.Middleware;

public class PermissionFilter : IAsyncActionFilter
{
    private readonly string _permissionCode;
    private readonly string? _scopeType;
    private readonly string? _scopeParamName;
    private readonly IHttpClientFactory _httpClientFactory;

    public PermissionFilter(string permissionCode, string? scopeType, string? scopeParamName, IHttpClientFactory httpClientFactory)
    {
        _permissionCode = permissionCode;
        _scopeType = scopeType;
        _scopeParamName = scopeParamName;
        _httpClientFactory = httpClientFactory;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user?.Identity == null || !user.Identity.IsAuthenticated)
        {
            context.HttpContext.Response.StatusCode = 401;
            return;
        }

        var employeeIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (employeeIdClaim == null)
        {
            context.HttpContext.Response.StatusCode = 403;
            return;
        }

        if (!int.TryParse(employeeIdClaim.Value, out var employeeId))
        {
            context.HttpContext.Response.StatusCode = 403;
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

        var client = _httpClientFactory.CreateClient("DatabaseAPI");
        var url = $"/api/admin/permissions/effective?employeeId={employeeId}&permissionCode={_permissionCode}";
        if (!string.IsNullOrEmpty(_scopeType)) url += $"&scopeType={_scopeType}";
        if (scopeId.HasValue) url += $"&scopeId={scopeId.Value}";

        try
        {
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                context.HttpContext.Response.StatusCode = 403;
                return;
            }

            var content = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("allowed", out var allowed) && allowed.GetBoolean())
            {
                await next();
                return;
            }
            else
            {
                context.HttpContext.Response.StatusCode = 403;
                return;
            }
        }
        catch
        {
            context.HttpContext.Response.StatusCode = 500;
            return;
        }
    }
}
