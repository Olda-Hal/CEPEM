using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.Models;
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
                .Where(e => e.Id == id && e.Person.Active)
                .Select(e => new EmployeeAuthInfo
                {
                    EmployeeId = e.Id,
                    PersonId = e.PersonId,
                    FirstName = e.Person.FirstName,
                    LastName = e.Person.LastName,
                    Email = e.Person.Email,
                    PasswordHash = "", // Don't return password hash
                    Salt = "", // Don't return salt
                    Active = e.Person.Active,
                    UID = e.Person.UID,
                    TitleBefore = e.Person.TitleBefore,
                    TitleAfter = e.Person.TitleAfter,
                    LastLoginAt = e.LastLoginAt
                })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return NotFound("Employee not found");
            }

            return Ok(employee);
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
