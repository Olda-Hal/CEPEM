using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace DatabaseAPI.Middleware
{
    public class RequireRoleAttribute : AuthorizeAttribute
    {
        public RequireRoleAttribute(string role) : base()
        {
            Roles = role;
        }
    }
}
