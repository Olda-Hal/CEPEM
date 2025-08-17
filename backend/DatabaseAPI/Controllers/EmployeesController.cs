using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeesController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public EmployeesController(DatabaseContext context)
        {
            _context = context;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<EmployeeAuthInfo>> GetEmployee(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.Person)
                .ThenInclude(p => p.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(e => e.Id == id && e.Person.Active)
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return NotFound("Employee not found");
            }

            var employeeAuthInfo = new EmployeeAuthInfo
            {
                EmployeeId = employee.Id,
                PersonId = employee.PersonId,
                FirstName = employee.Person.FirstName,
                LastName = employee.Person.LastName,
                Email = employee.Person.Email,
                PasswordHash = "", // Don't return password hash
                Salt = "", // Don't return salt
                Active = employee.Person.Active,
                UID = employee.Person.UID,
                TitleBefore = employee.Person.TitleBefore,
                TitleAfter = employee.Person.TitleAfter,
                LastLoginAt = employee.LastLoginAt,
                Roles = employee.Person.UserRoles.Select(ur => ur.Role.Name).ToList()
            };

            return Ok(employeeAuthInfo);
        }

        [HttpGet("dashboard-stats/{employeeId}")]
        public async Task<ActionResult<object>> GetDashboardStats(int employeeId)
        {
            var employee = await _context.Employees
                .Include(e => e.Person)
                .Where(e => e.Id == employeeId && e.Person.Active)
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return NotFound("Employee not found");
            }

            var totalEmployees = await _context.Employees
                .Include(e => e.Person)
                .CountAsync(e => e.Person.Active);

            return Ok(new
            {
                TotalEmployees = totalEmployees,
                LastLogin = employee.LastLoginAt,
                SystemStatus = "Online"
            });
        }
    }
}
