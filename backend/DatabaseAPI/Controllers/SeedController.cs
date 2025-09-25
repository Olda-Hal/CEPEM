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
            if (await _context.Employees.AnyAsync(e => e.Role == "Admin"))
                return BadRequest("Admin already exists.");

            var password = "Admin123!";
            var passwordHash = HashPassword(password);
            var admin = new Employee
            {
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@cepem.local",
                Role = "Admin",
                PasswordHash = passwordHash,
                PasswordChangedAt = new DateTime(2000, 1, 1), // old date
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.Employees.Add(admin);
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
