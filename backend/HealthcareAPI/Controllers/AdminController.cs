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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, IHttpClientFactory httpClientFactory, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [RequirePermission("employees.read")]
        [HttpGet("employees")]
        [PermissionDisplayName("List Employees")]
        public async Task<ActionResult<List<EmployeeListItem>>> GetAllEmployees()
        {
            try
            {
                var employees = await _adminService.GetAllEmployeesAsync();
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
