using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeedController : ControllerBase
    {
        private readonly DatabaseContext _context;
        public SeedController(DatabaseContext context)
        {
            _context = context;
        }

        [HttpPost("admin")]
        public async Task<IActionResult> SeedAdmin()
        {
            // Check if admin already exists
            var existingAdmin = await _context.Employees
                .Include(e => e.Person)
                .ThenInclude(p => p.UserRoles)
                .ThenInclude(ur => ur.Role)
                .AnyAsync(e => e.Person.UserRoles.Any(ur => ur.Role.Name == "SysAdmin"));
                
            if (existingAdmin)
                return BadRequest("Admin already exists.");

            // Get or create SysAdmin role
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "SysAdmin");
            if (adminRole == null)
            {
                adminRole = new Role { Name = "SysAdmin" };
                _context.Roles.Add(adminRole);
                await _context.SaveChangesAsync();
            }

            // Create Person for admin
            var adminPerson = new Person
            {
                FirstName = "Admin",
                LastName = "User", 
                Email = "admin@cepem.local",
                PhoneNumber = "+420000000000",
                UID = Guid.NewGuid().ToString(),
                Active = true,
                Gender = "M",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Persons.Add(adminPerson);
            await _context.SaveChangesAsync();

            var password = "Admin123!";
            var passwordHash = HashPassword(password);
            
            // Create Employee
            var adminEmployee = new Employee
            {
                PersonId = adminPerson.Id,
                PasswordHash = passwordHash,
                Salt = "",
                PasswordExpiration = new DateTime(2000, 1, 1),
                LastLoginAt = null
            };
            
            _context.Employees.Add(adminEmployee);
            await _context.SaveChangesAsync();

            // Create UserRole
            var userRole = new UserRole
            {
                UserId = adminPerson.Id,
                RoleId = adminRole.Id
            };
            
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();
            
            return Ok("Admin user created with password: " + password);
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
