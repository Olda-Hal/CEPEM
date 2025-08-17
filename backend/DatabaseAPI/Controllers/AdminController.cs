using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.APIModels;
using DatabaseAPI.Services;

namespace DatabaseAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IEmployeeManagementService _employeeManagementService;

        public AdminController(IEmployeeManagementService employeeManagementService)
        {
            _employeeManagementService = employeeManagementService;
        }

        [HttpGet("employees")]
        public async Task<ActionResult<List<EmployeeListItem>>> GetAllEmployees()
        {
            var employees = await _employeeManagementService.GetAllEmployeesAsync();
            return Ok(employees);
        }

        [HttpGet("employees/{employeeId}")]
        public async Task<ActionResult<EmployeeListItem>> GetEmployee(int employeeId)
        {
            var employee = await _employeeManagementService.GetEmployeeByIdAsync(employeeId);
            if (employee == null)
            {
                return NotFound("Employee not found");
            }
            return Ok(employee);
        }

        [HttpPut("employees/{employeeId}")]
        public async Task<ActionResult<UpdateEmployeeResponse>> UpdateEmployee(int employeeId, UpdateEmployeeRequest request)
        {
            var result = await _employeeManagementService.UpdateEmployeeAsync(employeeId, request);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpPatch("employees/{employeeId}/deactivate")]
        public async Task<ActionResult> DeactivateEmployee(int employeeId)
        {
            var success = await _employeeManagementService.DeactivateEmployeeAsync(employeeId);
            if (!success)
            {
                return NotFound("Employee not found");
            }
            return Ok(new { message = "Employee deactivated successfully" });
        }

        [HttpGet("roles")]
        public async Task<ActionResult<List<RoleDto>>> GetAllRoles()
        {
            var roles = await _employeeManagementService.GetAllRolesAsync();
            return Ok(roles);
        }
    }
}
