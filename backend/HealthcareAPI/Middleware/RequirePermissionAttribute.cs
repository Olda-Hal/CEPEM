using Microsoft.AspNetCore.Mvc;

namespace HealthcareAPI.Middleware;

public class RequirePermissionAttribute : TypeFilterAttribute
{
    /// <param name="permissionCode">Permission code e.g. "patient.read"</param>
    /// <param name="scopeType">Optional scope type name, e.g. "Hospital"</param>
    /// <param name="scopeParamName">Optional route/query param name to read scope id from</param>
    public RequirePermissionAttribute(string permissionCode, string? scopeType = null, string? scopeParamName = null) : base(typeof(PermissionFilter))
    {
        Arguments = new object[] { permissionCode, scopeType, scopeParamName };
    }
}
