using Microsoft.AspNetCore.Authorization;

namespace HealthcareAPI.Middleware
{
    public class RequireRoleAttribute : AuthorizeAttribute
    {
        public RequireRoleAttribute(params string[] roles)
        {
            Roles = string.Join(",", roles);
        }
    }
}
