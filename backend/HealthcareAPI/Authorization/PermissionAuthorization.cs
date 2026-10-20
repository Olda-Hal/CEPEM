using HealthcareAPI.Middleware;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace HealthcareAPI.Authorization;

/// <summary>
/// Requirement carrying a permission code (e.g. "patients.read").
/// Used by YARP routes via authorization policies named "perm:{code}" and
/// available to MVC actions as an alternative to [RequirePermission].
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode;
    }

    public string PermissionCode { get; }
}

/// <summary>
/// Shared permission evaluation against the DatabaseAPI effective-permission
/// endpoint. Used by both the authorization handler (YARP routes) and
/// PermissionFilter (MVC actions).
/// </summary>
public interface IPermissionEvaluator
{
    Task<bool?> IsAllowedAsync(int employeeId, string permissionCode, CancellationToken cancellationToken);
}

public class PermissionEvaluator : IPermissionEvaluator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PermissionEvaluator> _logger;

    public PermissionEvaluator(IHttpClientFactory httpClientFactory, ILogger<PermissionEvaluator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool?> IsAllowedAsync(int employeeId, string permissionCode, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("DatabaseAPI");
        var url = $"/api/admin/permissions/effective?employeeId={employeeId}&permissionCode={Uri.EscapeDataString(permissionCode)}";
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Permission evaluation failed for permission {PermissionCode}, employee {EmployeeId}: status {StatusCode}",
                    permissionCode, employeeId, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(content);
            return doc.RootElement.TryGetProperty("allowed", out var allowed) && allowed.GetBoolean();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating permission {PermissionCode} for employee {EmployeeId}", permissionCode, employeeId);
            return null;
        }
    }
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionEvaluator _permissionEvaluator;

    public PermissionAuthorizationHandler(IPermissionEvaluator permissionEvaluator)
    {
        _permissionEvaluator = permissionEvaluator;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext || httpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var employeeIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (employeeIdClaim == null || !int.TryParse(employeeIdClaim, out var employeeId))
        {
            return;
        }

        var allowed = await _permissionEvaluator.IsAllowedAsync(employeeId, requirement.PermissionCode, httpContext.RequestAborted);
        if (allowed == true)
        {
            context.Succeed(requirement);
        }
    }
}
