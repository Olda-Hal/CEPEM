using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HealthcareAPI.Models;
using HealthcareAPI.Services;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;

        public EmployeesController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        [HttpGet("me")]
        public async Task<ActionResult<Employee>> GetCurrentEmployee()
        {
            var employeeId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var employee = await _employeeService.GetCurrentEmployeeAsync(employeeId);

            if (employee == null)
            {
                return NotFound("Employee not found");
            }

            return Ok(employee);
        }

        [HttpGet("dashboard-stats")]
        public async Task<ActionResult<object>> GetDashboardStats()
        {
            var employeeId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var stats = await _employeeService.GetDashboardStatsAsync(employeeId);

            if (stats == null)
            {
                return NotFound("Employee not found");
            }

            return Ok(stats);
        }
    }
}
