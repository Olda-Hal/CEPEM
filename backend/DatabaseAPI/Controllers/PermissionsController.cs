using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers
{
    [ApiController]
    [Route("api/admin/permissions")]
    public class PermissionsController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public PermissionsController(DatabaseContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<Permission>>> GetAll()
        {
            return Ok(await _context.Permissions.ToListAsync());
        }

        [HttpPost]
        public async Task<ActionResult<Permission>> Create(Permission p)
        {
            _context.Permissions.Add(p);
            await _context.SaveChangesAsync();
            return Ok(p);
        }

        [HttpGet("/api/admin/employee-permissions")]
        public async Task<ActionResult<List<EmployeePermission>>> GetEmployeePermissions([FromQuery] int employeeId)
        {
            var employee = await _context.Employees.Include(e => e.Person).FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return NotFound("Employee not found");
            var personId = employee.PersonId;
            var perms = await _context.EmployeePermissions.Where(ep => ep.UserId == personId).ToListAsync();
            return Ok(perms);
        }

        public class RolePermissionDto
        {
            public int PermissionId { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Effect { get; set; } = "Allow";
        }

        [HttpGet("/api/admin/role-permissions")]
        public async Task<ActionResult<List<RolePermissionDto>>> GetRolePermissions([FromQuery] int roleId)
        {
            var role = await _context.Roles.FindAsync(roleId);
            if (role == null) return NotFound("Role not found");

            var perms = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Join(_context.Permissions,
                    rp => rp.PermissionId,
                    p => p.Id,
                    (rp, p) => new RolePermissionDto { PermissionId = p.Id, Code = p.Code, Effect = rp.Effect })
                .ToListAsync();
            return Ok(perms);
        }

        public class UpdateRolePermissionsRequest
        {
            public List<RolePermissionDto> Permissions { get; set; } = new();
        }

        [HttpPut("/api/admin/role-permissions")]
        public async Task<ActionResult> UpdateRolePermissions([FromQuery] int roleId, UpdateRolePermissionsRequest request)
        {
            var role = await _context.Roles.FindAsync(roleId);
            if (role == null) return NotFound("Role not found");

            var existing = await _context.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();
            _context.RolePermissions.RemoveRange(existing);

            var validPermissionIds = await _context.Permissions.Select(p => p.Id).ToListAsync();
            foreach (var perm in request.Permissions)
            {
                if (!validPermissionIds.Contains(perm.PermissionId))
                {
                    return BadRequest($"Unknown permission id: {perm.PermissionId}");
                }
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = perm.PermissionId,
                    Effect = perm.Effect.Equals("Deny", StringComparison.OrdinalIgnoreCase) ? "Deny" : "Allow"
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Role permissions updated" });
        }

        public class CreateEmployeePermissionRequest
        {
            public int EmployeeId { get; set; }
            public int PermissionId { get; set; }
            public string Effect { get; set; } = "Allow";
            public string? ScopeType { get; set; }
            public int? ScopeId { get; set; }
        }

        [HttpPost("/api/admin/employee-permissions")]
        public async Task<ActionResult<EmployeePermission>> CreateEmployeePermission(CreateEmployeePermissionRequest req)
        {
            var employee = await _context.Employees.Include(e => e.Person).FirstOrDefaultAsync(e => e.Id == req.EmployeeId);
            if (employee == null) return NotFound("Employee not found");

            var ep = new EmployeePermission
            {
                UserId = employee.PersonId,
                PermissionId = req.PermissionId,
                Effect = req.Effect,
                ScopeType = req.ScopeType,
                ScopeId = req.ScopeId
            };

            _context.EmployeePermissions.Add(ep);
            await _context.SaveChangesAsync();
            return Ok(ep);
        }

        [HttpDelete("/api/admin/employee-permissions/{id}")]
        public async Task<ActionResult> DeleteEmployeePermission(int id)
        {
            var ep = await _context.EmployeePermissions.FindAsync(id);
            if (ep == null) return NotFound();
            _context.EmployeePermissions.Remove(ep);
            await _context.SaveChangesAsync();
            return Ok();
        }

        /// <summary>
        /// Evaluate effective permission for an employee and optional scope.
        /// Query params: employeeId, permissionCode, scopeType (optional), scopeId (optional)
        /// </summary>
        [HttpGet("/api/admin/permissions/effective")]
        public async Task<ActionResult<object>> Effective([FromQuery] int employeeId, [FromQuery] string permissionCode, [FromQuery] string? scopeType = null, [FromQuery] int? scopeId = null)
        {
            // Find employee and their person id
            var employee = await _context.Employees.Include(e => e.Person).FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return NotFound("Employee not found");

            var personId = employee.PersonId;

            var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Code == permissionCode);
            if (permission == null) return NotFound("Permission not found");

            // 1) Check explicit employee-level permissions matching scope
            var employeePerms = await _context.EmployeePermissions
                .Where(ep => ep.UserId == personId && ep.PermissionId == permission.Id)
                .ToListAsync();

            // exact scope match or global (ScopeType null)
            var matchingEmployeePerms = employeePerms.Where(ep =>
                (string.IsNullOrEmpty(ep.ScopeType) && ep.ScopeId == null) ||
                (!string.IsNullOrEmpty(scopeType) && ep.ScopeType == scopeType && (ep.ScopeId == null || ep.ScopeId == scopeId))
            ).ToList();

            if (matchingEmployeePerms.Any(mp => mp.Effect.Equals("Deny", System.StringComparison.OrdinalIgnoreCase)))
            {
                return Ok(new { allowed = false, reason = "Explicit employee deny" });
            }

            if (matchingEmployeePerms.Any(mp => mp.Effect.Equals("Allow", System.StringComparison.OrdinalIgnoreCase)))
            {
                return Ok(new { allowed = true, reason = "Explicit employee allow" });
            }

            // 2) Check role-based permissions
            var roleIds = await _context.UserRoles.Where(ur => ur.UserId == personId).Select(ur => ur.RoleId).ToListAsync();
            var rolePerms = await _context.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId) && rp.PermissionId == permission.Id).ToListAsync();

            if (rolePerms.Any(rp => rp.Effect.Equals("Deny", System.StringComparison.OrdinalIgnoreCase)))
            {
                return Ok(new { allowed = false, reason = "Role deny" });
            }

            if (rolePerms.Any(rp => rp.Effect.Equals("Allow", System.StringComparison.OrdinalIgnoreCase)))
            {
                return Ok(new { allowed = true, reason = "Role allow" });
            }

            // Default deny
            return Ok(new { allowed = false, reason = "Default deny" });
        }
    }
}
