using HealthcareAPI.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HealthcareAPI.Middleware;

/// <summary>
/// Requires the authenticated employee to hold the given permission code
/// (evaluated against DatabaseAPI). Optional scope support: the scope id is
/// read from a route value or query string.
/// </summary>
public class RequirePermissionAttribute : Attribute, IFilterFactory
{
    private readonly string _permissionCode;
    private readonly string? _scopeType;
    private readonly string? _scopeParamName;

    /// <param name="permissionCode">Permission code, e.g. "patient.read"</param>
    /// <param name="scopeType">Optional scope type name, e.g. "Hospital"</param>
    /// <param name="scopeParamName">Optional route/query param name to read the scope id from</param>
    public RequirePermissionAttribute(string permissionCode, string? scopeType = null, string? scopeParamName = null)
    {
        _permissionCode = permissionCode;
        _scopeType = scopeType;
        _scopeParamName = scopeParamName;
    }

    // Not reusable: the filter resolves scoped services from the per-request provider
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new PermissionFilter(
            _permissionCode,
            _scopeType,
            _scopeParamName,
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            serviceProvider.GetRequiredService<ILogger<PermissionFilter>>());
    }
}
