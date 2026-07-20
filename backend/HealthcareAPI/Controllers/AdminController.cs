using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HealthcareAPI.Models;
using HealthcareAPI.Services;
using HealthcareAPI.Attributes;
using HealthcareAPI.Middleware;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IPermissionCatalogService _permissionCatalogService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, IPermissionCatalogService permissionCatalogService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _permissionCatalogService = permissionCatalogService;
            _logger = logger;
        }

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

        [HttpPut("employees/{employeeId}")]
        [PermissionDisplayName("Update Employee")]
        public async Task<ActionResult<UpdateEmployeeResponse>> UpdateEmployee(int employeeId, UpdateEmployeeRequest request)
        {
            try
            {
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

        [HttpPatch("employees/{employeeId}/deactivate")]
        [PermissionDisplayName("Deactivate Employee")]
        public async Task<ActionResult> DeactivateEmployee(int employeeId)
        {
            try
            {
                var success = await _adminService.DeactivateEmployeeAsync(employeeId);
                if (!success)
                {
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

        [HttpGet("access/permissions")]
        [PermissionDisplayName("List Permission Catalog")]
        [RequireRole("SysAdmin")]
        public ActionResult<List<EndpointPermissionCatalogItem>> GetPermissionCatalog()
        {
            var catalog = _permissionCatalogService.GetCatalog();
            return Ok(catalog);
        }

        [HttpGet("access/employees/{employeeId}/rules")]
        [PermissionDisplayName("View Employee Access Rules")]
        [RequireRole("SysAdmin")]
        public async Task<ActionResult<EmployeePermissionRulesResponse>> GetEmployeePermissionRules(int employeeId)
        {
            try
            {
                var rules = await _adminService.GetEmployeePermissionRulesAsync(employeeId);
                if (rules == null)
                    return NotFound("Employee rules not found");

                return Ok(rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee access rules");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("access/employees/{employeeId}/rules")]
        [PermissionDisplayName("Update Employee Access Rules")]
        [RequireRole("SysAdmin")]
        public async Task<ActionResult> UpdateEmployeePermissionRules(int employeeId, [FromBody] UpdateEmployeePermissionRulesRequest request)
        {
            try
            {
                var success = await _adminService.UpdateEmployeePermissionRulesAsync(employeeId, request);
                if (!success)
                    return NotFound("Employee rules not found");

                return Ok(new { message = "Employee access rules updated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee access rules");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("access/roles/{roleId}/rules")]
        [PermissionDisplayName("View Role Access Rules")]
        [RequireRole("SysAdmin")]
        public async Task<ActionResult<RolePermissionRulesResponse>> GetRolePermissionRules(int roleId)
        {
            try
            {
                var rules = await _adminService.GetRolePermissionRulesAsync(roleId);
                if (rules == null)
                    return NotFound("Role rules not found");

                return Ok(rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting role access rules");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("access/roles/{roleId}/rules")]
        [PermissionDisplayName("Update Role Access Rules")]
        [RequireRole("SysAdmin")]
        public async Task<ActionResult> UpdateRolePermissionRules(int roleId, [FromBody] UpdateRolePermissionRulesRequest request)
        {
            try
            {
                var success = await _adminService.UpdateRolePermissionRulesAsync(roleId, request);
                if (!success)
                    return NotFound("Role rules not found");

                return Ok(new { message = "Role access rules updated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role access rules");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
