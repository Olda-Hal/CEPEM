using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HealthcareAPI.Models;
using HealthcareAPI.Services;
using HealthcareAPI.Attributes;
using HealthcareAPI.Middleware;
using System.Security.Claims;
using System.Text.Json;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IAuthService _authService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, IAuthService authService, IHttpClientFactory httpClientFactory, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _authService = authService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [RequirePermission("employees.read")]
        [HttpGet("employees")]
        [PermissionDisplayName("List Employees")]
        public async Task<ActionResult<List<EmployeeListItem>>> GetAllEmployees([FromQuery] bool includeInactive = false)
        {
            try
            {
                var employees = await _adminService.GetAllEmployeesAsync(includeInactive);
                return Ok(employees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all employees");
                return StatusCode(500, "Internal server error");
            }
        }

        [RequirePermission("employees.read")]
        [HttpGet("employees/{employeeId}")]
        [PermissionDisplayName("View Employee Detail")]
        public async Task<ActionResult<EmployeeListItem>> GetEmployee(int employeeId)
        {
            try
            {
                var employee = await _adminService.GetEmployeeByIdAsync(employeeId);
                if (employee == null)
                {
                    return NotFound("Employee not found");
                }
                return Ok(employee);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting employee {employeeId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [RequirePermission("employees.manage")]
        [HttpPut("employees/{employeeId}")]
        [PermissionDisplayName("Update Employee")]
        public async Task<ActionResult<UpdateEmployeeResponse>> UpdateEmployee(int employeeId, UpdateEmployeeRequest request)
        {
            try
            {
                // Scope enforcement (Center/Country Admin) happens in DatabaseAPI;
                // it validates the target visibility, assignable roles and assignments.
                var result = await _adminService.UpdateEmployeeAsync(employeeId, request);
                if (!result.Success)
                {
                    return BadRequest(result);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating employee {employeeId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [RequirePermission("employees.manage")]
        [HttpPatch("employees/{employeeId}/deactivate")]
        [PermissionDisplayName("Deactivate Employee")]
        public async Task<ActionResult> DeactivateEmployee(int employeeId)
        {
            try
            {
                var (success, errorBody) = await _adminService.DeactivateEmployeeAsync(employeeId);
                if (!success)
                {
                    if (errorBody != null)
                    {
                        // Scope violation or another DB-side reason (e.g. 403 from DatabaseAPI)
                        return StatusCode(403, errorBody);
                    }
                    return NotFound("Employee not found");
                }
                return Ok(new { message = "Employee deactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating employee {employeeId}");
                return StatusCode(500, "Internal server error");
            }
        }

        [RequirePermission("employees.manage")]
        [HttpPost("employees/{employeeId}/reset-password")]
        [PermissionDisplayName("Trigger Password Reset E-mail")]
        public async Task<IActionResult> TriggerEmployeePasswordReset(int employeeId)
        {
            try
            {
                var (success, errorBody) = await _adminService.TriggerPasswordResetAsync(employeeId);
                if (!success)
                {
                    return errorBody == "Employee not found" ? NotFound(errorBody) : BadRequest(errorBody ?? "Reset e-mail se nepodařilo odeslat");
                }
                return Ok(new { message = "Resetový odkaz byl odeslán na e-mail zaměstnance." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error triggering password reset for employee {employeeId}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// SysAdmin-only "preview as employee": issues a short-lived token for
        /// the target account so the administrator can see exactly what that
        /// employee can do. Nested previews and previews of your own account
        /// are refused.
        /// </summary>
        [RequireRole("SysAdmin")]
        [HttpPost("employees/{employeeId}/impersonate")]
        [PermissionDisplayName("Preview As Employee")]
        public async Task<ActionResult<ImpersonationResponse>> ImpersonateEmployee(int employeeId)
        {
            // A token that is already a preview must not start another one.
            if (User.FindFirst("impersonator_id") != null)
            {
                return BadRequest("Náhled nelze spustit z režimu náhledu.");
            }

            var actorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (actorIdClaim == null || !int.TryParse(actorIdClaim.Value, out var actorId))
            {
                return Unauthorized();
            }

            var actorName = User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value
                ?? "SysAdmin";

            if (employeeId == actorId)
            {
                return BadRequest("Vlastní účet nelze otevřít v náhledu.");
            }

            var result = await _authService.ImpersonateAsync(employeeId, actorId, actorName);
            if (result == null)
            {
                return NotFound("Zaměstnanec neexistuje nebo je deaktivován.");
            }

            return Ok(result);
        }

        [RequirePermission("employees.read")]
        [HttpGet("employees/scope")]
        [PermissionDisplayName("View Own Admin Scope")]
        public async Task<IActionResult> GetActorScope()
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync("/api/admin/employees/scope");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving actor scope");
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }

        [RequirePermission("employees.read")]
        [HttpGet("roles")]
        [PermissionDisplayName("List Roles")]
        public async Task<ActionResult<List<RoleDto>>> GetAllRoles()
        {
            try
            {
                var roles = await _adminService.GetAllRolesAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all roles");
                return StatusCode(500, "Internal server error");
            }
        }

        [RequirePermission("roles.manage")]
        [HttpPost("roles")]
        [PermissionDisplayName("Create Role")]
        [RequireRole("SysAdmin")]
        public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest request)
        {
            try
            {
                var role = await _adminService.CreateRoleAsync(request);
                if (role == null)
                    return BadRequest("Role already exists or is invalid");

                return Ok(role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return StatusCode(500, "Internal server error");
            }
        }

        [RequirePermission("roles.manage")]
        [HttpDelete("roles/{roleId}")]
        [PermissionDisplayName("Delete Role")]
        [RequireRole("SysAdmin")]
        public async Task<IActionResult> DeleteRole(int roleId)
        {
            try
            {
                var (success, errorBody) = await _adminService.DeleteRoleAsync(roleId);
                if (!success)
                {
                    return errorBody == "Role not found" ? NotFound(errorBody) : Conflict(errorBody);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role {RoleId}", roleId);
                return StatusCode(500, "Internal server error");
            }
        }

        [RequirePermission("roles.manage")]
        [HttpPut("roles/{roleId}/default-path")]
        [PermissionDisplayName("Set Role Default Page")]
        [RequireRole("SysAdmin")]
        public async Task<IActionResult> UpdateRoleDefaultPath(int roleId, [FromBody] UpdateRoleDefaultPathRequest request)
        {
            try
            {
                var (success, errorBody) = await _adminService.UpdateRoleDefaultPathAsync(roleId, request.DefaultPath);
                if (!success)
                {
                    return errorBody == "Role not found" ? NotFound(errorBody) : BadRequest(errorBody);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating default path of role {RoleId}", roleId);
                return StatusCode(500, "Internal server error");
            }
        }

        [RequirePermission("permissions.manage")]
        [HttpGet("access/permissions")]
        public async Task<IActionResult> GetPermissionCatalog()
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync("/api/admin/permissions");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving permission catalog");
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }

        [RequirePermission("permissions.manage")]
        [HttpGet("access/employees/{employeeId}/rules")]
        public async Task<IActionResult> GetEmployeePermissionRules(int employeeId)
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/admin/employee-permissions?employeeId={employeeId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound("Employee not found");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving employee permissions");
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }

        [RequirePermission("permissions.manage")]
        [HttpPost("access/employees/{employeeId}/permissions")]
        public async Task<IActionResult> AddEmployeePermission(int employeeId, [FromBody] JsonElement request)
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var payload = JsonSerializer.Serialize(request);
            var body = JsonDocument.Parse(payload).RootElement;
            var permissionId = body.TryGetProperty("permissionId", out var pid) ? pid.GetInt32() : 0;
            var effect = body.TryGetProperty("effect", out var eff) ? eff.GetString() : "Allow";
            var scopeType = body.TryGetProperty("scopeType", out var st) ? st.GetString() : null;
            var scopeId = body.TryGetProperty("scopeId", out var sid) && sid.ValueKind == JsonValueKind.Number ? sid.GetInt32() : (int?)null;

            var dbRequest = JsonSerializer.Serialize(new { employeeId, permissionId, effect, scopeType, scopeId });
            var response = await client.PostAsync("/api/admin/employee-permissions",
                new StringContent(dbRequest, System.Text.Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error adding employee permission");
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }

        [RequirePermission("permissions.manage")]
        [HttpDelete("access/employee-permissions/{id}")]
        public async Task<IActionResult> DeleteEmployeePermission(int id)
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.DeleteAsync($"/api/admin/employee-permissions/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound("Employee permission not found");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error deleting employee permission");
            return Ok(new { message = "Employee permission deleted" });
        }

        [RequirePermission("permissions.manage")]
        [HttpGet("access/roles/{roleId}/rules")]
        public async Task<IActionResult> GetRolePermissionRules(int roleId)
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/admin/role-permissions?roleId={roleId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound("Role not found");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving role permissions");
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }

        [RequirePermission("permissions.manage")]
        [HttpPut("access/roles/{roleId}/rules")]
        public async Task<IActionResult> UpdateRolePermissionRules(int roleId, [FromBody] JsonElement request)
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.PutAsync($"/api/admin/role-permissions?roleId={roleId}",
                new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error updating role permissions");
            return Ok(new { message = "Role permissions updated" });
        }
    }
}
