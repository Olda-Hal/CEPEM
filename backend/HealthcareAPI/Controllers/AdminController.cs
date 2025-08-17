using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HealthcareAPI.Models;
using HealthcareAPI.Services;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        [HttpGet("employees")]
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
    }
}
