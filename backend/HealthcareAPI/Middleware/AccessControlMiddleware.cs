using System.Security.Claims;
using HealthcareAPI.Models;
using HealthcareAPI.Services;
using Microsoft.AspNetCore.Authorization;

namespace HealthcareAPI.Middleware;

public class AccessControlMiddleware
{
    private static readonly HashSet<string> CountryAdminAlwaysAllowedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET:/api/auth/next-uid",
        "POST:/api/auth/create-employee",
        "GET:/api/hospitals",
        "POST:/api/hospitals",
        "DELETE:/api/hospitals/{hospitalid}",
        "GET:/api/examinationtypes",
        "GET:/api/admin/employees",
        "GET:/api/admin/roles",
        "PUT:/api/admin/employees/{employeeid}",
        "PATCH:/api/admin/employees/{employeeid}/deactivate",
        "GET:/api/examinations",
        "GET:/api/examinations/export"
    };

    private static readonly HashSet<string> DoctorAlwaysAllowedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET:/api/hospitals",
        "GET:/api/examinations",
        "GET:/api/examinations/export"
    };

    private readonly RequestDelegate _next;

    public AccessControlMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAccessControlService accessControlService, IPermissionCatalogService permissionCatalogService)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            await _next(context);
            return;
        }

        var requiresAuth = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any();
        if (!requiresAuth)
        {
            await _next(context);
            return;
        }

        var employeeIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(employeeIdClaim, out var employeeId) || employeeId <= 0)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        var permissionKey = permissionCatalogService.BuildPermissionKey(context);
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Forbidden", reason = "Permission key not resolved" });
            return;
        }

        if (context.User.IsInRole("Country Admin") && CountryAdminAlwaysAllowedPermissions.Contains(permissionKey))
        {
            await _next(context);
            return;
        }

        if (context.User.IsInRole("Doctor") && DoctorAlwaysAllowedPermissions.Contains(permissionKey))
        {
            await _next(context);
            return;
        }

        var aclVersionClaim = context.User.FindFirst("acl_version")?.Value;
        if (!string.IsNullOrWhiteSpace(aclVersionClaim) && int.TryParse(aclVersionClaim, out var aclVersionFromToken))
        {
            // The backend still evaluates against fresh DB state. Token version is kept for future refresh optimizations.
            _ = aclVersionFromToken;
        }

        var evaluation = await accessControlService.EvaluateAsync(new AccessEvaluationRequest
        {
            EmployeeId = employeeId,
            PermissionKey = permissionKey,
            ResourceContexts = await permissionCatalogService.ExtractResourceContextsAsync(context)
        }, context.RequestAborted);

        if (!evaluation.Allowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Forbidden", reason = evaluation.Reason, permission = permissionKey });
            return;
        }

        await _next(context);
    }
}
