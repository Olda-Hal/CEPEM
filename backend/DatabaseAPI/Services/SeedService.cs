using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Services
{
    public interface ISeedService
    {
        Task SeedAsync();
    }

    public class SeedService : ISeedService
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<SeedService> _logger;

        public SeedService(DatabaseContext context, ILogger<SeedService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                await SeedRolesAsync();
                await SeedAdminUserAsync();
                await _context.SaveChangesAsync();
                _logger.LogInformation("Database seeded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database seeding");
                throw;
            }
        }

        private async Task SeedRolesAsync()
        {
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "SysAdmin");
            if (adminRole == null)
            {
                adminRole = new Role { Name = "SysAdmin" };
                _context.Roles.Add(adminRole);
                await _context.SaveChangesAsync(); // Save to get the ID
                _logger.LogInformation("SysAdmin role created.");
            }
        }

        private async Task SeedAdminUserAsync()
        {
            // Check if admin already exists
            var existingAdmin = await _context.Employees
                .Include(e => e.Person)
                .ThenInclude(p => p.UserRoles)
                .ThenInclude(ur => ur.Role)
                .AnyAsync(e => e.Person.UserRoles.Any(ur => ur.Role.Name == "SysAdmin"));

            if (existingAdmin)
            {
                _logger.LogInformation("SysAdmin user already exists, skipping creation.");
                return;
            }

            // Get SysAdmin role
            var adminRole = await _context.Roles.FirstAsync(r => r.Name == "SysAdmin");

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
            await _context.SaveChangesAsync(); // Save to get PersonId

            // Create Employee for admin
            var password = "admin";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var adminEmployee = new Employee
            {
                PersonId = adminPerson.Id,
                PasswordHash = passwordHash,
                Salt = "", // BCrypt doesn't need separate salt
                PasswordExpiration = new DateTime(2000, 1, 1), // Old date to force password change
                LastLoginAt = null
            };

            _context.Employees.Add(adminEmployee);
            await _context.SaveChangesAsync(); // Save to get EmployeeId

            // Create UserRole to assign Administrator role to the person
            var userRole = new UserRole
            {
                UserId = adminPerson.Id,
                RoleId = adminRole.Id
            };

            _context.UserRoles.Add(userRole);

            _logger.LogInformation("SysAdmin user created with email: {Email} and password: {Password}", 
                adminPerson.Email, password);
        }
    }
}
