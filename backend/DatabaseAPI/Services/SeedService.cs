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
                await SeedPermissionsAsync();
                await SeedIntakeFormEventTypeAsync();
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
            await EnsureRoleExistsAsync("SysAdmin");
            await EnsureRoleExistsAsync("Examiner");
            await EnsureRoleExistsAsync("Doctor");
            await EnsureRoleExistsAsync("Center Admin");
            await EnsureRoleExistsAsync("Country Admin");
        }

        private async Task EnsureRoleExistsAsync(string roleName)
        {
            var existingRole = await _context.Roles
                .Include(r => r.NameTranslation)
                .FirstOrDefaultAsync(r => r.NameTranslation != null && r.NameTranslation.EN == roleName);

            if (existingRole != null)
            {
                return;
            }

            var translation = new Translation
            {
                EN = roleName,
                CS = roleName
            };
            _context.Translations.Add(translation);
            await _context.SaveChangesAsync();

            _context.Roles.Add(new Role { NameTranslationId = translation.Id });
            await _context.SaveChangesAsync();

            _logger.LogInformation("{RoleName} role created.", roleName);
        }

        private async Task<Role?> GetRoleByNameAsync(string roleName)
        {
            return await _context.Roles
                .Include(r => r.NameTranslation)
                .FirstOrDefaultAsync(r => r.NameTranslation != null && r.NameTranslation.EN == roleName);
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
            var password = Environment.GetEnvironmentVariable("ADMIN_INITIAL_PASSWORD");
            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning(
                    "No SysAdmin user exists and ADMIN_INITIAL_PASSWORD is not set - skipping admin creation. " +
                    "Set ADMIN_INITIAL_PASSWORD and restart to create the initial admin account.");
                return;
            }
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

            _logger.LogInformation("SysAdmin user created with email: {Email}. Change this password after first login.",
                "admin@cepem.local");
        }

        private async Task SeedIntakeFormEventTypeAsync()
        {
            var intakeType = await _context.EventTypes
                .Include(et => et.NameTranslation)
                .FirstOrDefaultAsync(et => et.NameTranslation != null &&
                    (et.NameTranslation.EN == "Intake Form" ||
                     et.NameTranslation.CS == "Vstupni Formular" ||
                     et.NameTranslation.CS == "Vstupní Formulář"));

            if (intakeType != null)
            {
                return;
            }

            var translation = new Translation
            {
                EN = "Intake Form",
                CS = "Vstupni Formular"
            };

            _context.Translations.Add(translation);
            await _context.SaveChangesAsync();

            _context.EventTypes.Add(new EventType
            {
                NameTranslationId = translation.Id
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("Intake Form event type created.");
        }

        // Permission catalog: codes are referenced by [RequirePermission] attributes
        // in HealthcareAPI controllers and granted to roles below.
        private static readonly (string Code, string Description)[] PermissionCatalog =
        {
            ("dashboard.read", "View own employee profile and dashboard statistics"),
            ("uid.read", "Look up the next available patient UID"),
            ("patients.read", "View and search patients, including photos"),
            ("patients.write", "Create patients and edit patient data/comments"),
            ("patients.photo.write", "Upload patient photos"),
            ("patients.documents.read", "View and download patient documents"),
            ("patients.documents.write", "Upload and delete patient documents"),
            ("events.read", "View event options (types, symptoms, drugs, ...)"),
            ("events.write", "Record patient events"),
            ("examinations.read", "View examinations"),
            ("examinations.export", "Export examinations"),
            ("examinations.documents.read", "View and download examination documents"),
            ("examinations.documents.write", "Upload and delete examination documents"),
            ("hospitals.read", "View hospitals"),
            ("hospitals.write", "Create, update and deactivate hospitals"),
            ("examinationtypes.read", "View examination types"),
            ("examinationtypes.write", "Manage hospital examination type assignments"),
            ("examinationtypes.manage", "Create, update and delete examination types"),
            ("examinationrooms.read", "View examination rooms"),
            ("examinationrooms.write", "Create, update and delete examination rooms"),
            ("doctorexaminationrooms.read", "View doctor examination room assignments"),
            ("doctorexaminationrooms.write", "Assign and remove doctor examination rooms"),
            ("reservations.read", "View reservation slots"),
            ("reservations.write", "Create, update, block, confirm and release reservation slots"),
            ("employees.read", "View employee and role lists"),
            ("employees.create", "Create employees"),
            ("employees.manage", "Update and deactivate employees"),
            ("roles.manage", "Create roles and manage role permissions"),
            ("permissions.manage", "Manage permissions and per-employee permission overrides"),
            ("settings.read", "View application settings"),
            ("settings.write", "Change application settings"),
            ("tests.read", "View the test results dashboard"),
            ("audit.read", "View the activity audit log")
        };

        private async Task SeedPermissionsAsync()
        {
            foreach (var (code, description) in PermissionCatalog)
            {
                var existing = await _context.Permissions.FirstOrDefaultAsync(p => p.Code == code);
                if (existing == null)
                {
                    _context.Permissions.Add(new Permission { Code = code, Description = description });
                }
                else if (existing.Description != description)
                {
                    existing.Description = description;
                }
            }
            await _context.SaveChangesAsync();

            await SeedRoleGrantsAsync("SysAdmin", PermissionCatalog.Select(p => p.Code).ToArray());
            await SeedRoleGrantsAsync("Examiner", new[]
            {
                "dashboard.read", "uid.read", "employees.read", "employees.create",
                "patients.read", "patients.write", "patients.photo.write",
                "patients.documents.read", "patients.documents.write",
                "events.read", "events.write",
                "examinations.read", "examinations.export", "examinations.documents.write"
            });
            await SeedRoleGrantsAsync("Doctor", new[]
            {
                "dashboard.read",
                "patients.read", "patients.documents.read",
                "examinations.read", "examinations.export", "examinations.documents.read",
                "hospitals.read"
            });
            await SeedRoleGrantsAsync("Center Admin", new[]
            {
                "dashboard.read",
                "hospitals.read", "hospitals.write",
                "examinationrooms.read", "examinationrooms.write",
                "reservations.read", "reservations.write",
                "examinations.read", "examinations.export"
            });
            await SeedRoleGrantsAsync("Country Admin", new[]
            {
                "dashboard.read", "uid.read",
                "employees.read", "employees.create", "employees.manage",
                "patients.read", "patients.write", "patients.photo.write",
                "patients.documents.read", "patients.documents.write",
                "hospitals.read", "hospitals.write",
                "examinationtypes.read", "examinationtypes.write",
                "examinationrooms.read", "examinationrooms.write",
                "doctorexaminationrooms.read", "doctorexaminationrooms.write",
                "reservations.read", "reservations.write",
                "examinations.read", "examinations.export"
            });
        }

        private async Task SeedRoleGrantsAsync(string roleName, string[] permissionCodes)
        {
            var role = await GetRoleByNameAsync(roleName);
            if (role == null)
            {
                _logger.LogWarning("Role {RoleName} not found, skipping permission grants.", roleName);
                return;
            }

            var permissions = await _context.Permissions
                .Where(p => permissionCodes.Contains(p.Code))
                .ToListAsync();

            var existingGrants = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .ToListAsync();
            var existingByPermission = existingGrants.ToDictionary(rp => rp.PermissionId);

            foreach (var permission in permissions)
            {
                if (!existingByPermission.ContainsKey(permission.Id))
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id,
                        Effect = "Allow"
                    });
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}
