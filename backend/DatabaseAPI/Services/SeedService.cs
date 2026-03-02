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
            var adminRole = await _context.Roles
                .Include(r => r.NameTranslation)
                .FirstOrDefaultAsync(r => r.NameTranslation != null && r.NameTranslation.EN == "SysAdmin");
            if (adminRole == null)
            {
                var translation = new Translation { EN = "SysAdmin" };
                _context.Translations.Add(translation);
                await _context.SaveChangesAsync();
                adminRole = new Role { NameTranslationId = translation.Id };
                _context.Roles.Add(adminRole);
                await _context.SaveChangesAsync();
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
                    .ThenInclude(r => r.NameTranslation)
                .AnyAsync(e => e.Person.UserRoles.Any(ur => ur.Role.NameTranslation != null && ur.Role.NameTranslation.EN == "SysAdmin"));

            if (existingAdmin)
            {
                _logger.LogInformation("SysAdmin user already exists, skipping creation.");
                return;
            }

            // Get SysAdmin role
            var adminRole = await _context.Roles
                .Include(r => r.NameTranslation)
                .FirstAsync(r => r.NameTranslation != null && r.NameTranslation.EN == "SysAdmin");

            // Create Person for admin
            var adminPerson = new Person
            {
                FirstName = "Admin",
                LastName = "User",
                UID = Guid.NewGuid().ToString(),
                Active = true,
                Gender = "M",
                CreatedAt = DateTime.UtcNow
            };

            _context.Persons.Add(adminPerson);
            await _context.SaveChangesAsync(); // Save to get PersonId

            // Create Contact with email and phone for admin
            var adminContact = new Contact();
            _context.Contacts.Add(adminContact);
            await _context.SaveChangesAsync();
            _context.ContactEmails.Add(new ContactEmail { ContactId = adminContact.Id, Email = "admin@cepem.local" });
            _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = adminContact.Id, PhoneNumber = "+420000000000" });
            _context.ContactToObjects.Add(new ContactToObject
            {
                ContactId = adminContact.Id,
                ObjectId = adminPerson.Id,
                ObjectType = ContactObjectType.Person,
                PersonId = adminPerson.Id
            });
            await _context.SaveChangesAsync();

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
                "admin@cepem.local", password);
        }
    }
}
