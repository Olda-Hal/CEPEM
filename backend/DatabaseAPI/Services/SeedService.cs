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
                await SeedAccessControlRulesAsync();
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

        private async Task SeedAccessControlRulesAsync()
        {
            await SeedSysAdminRulesAsync();
            await SeedRoleRulesIfEmptyAsync("Examiner", BuildExaminerRules());
            await SeedRoleRulesIfEmptyAsync("Doctor", BuildDoctorRules());
            await SeedRoleRulesIfEmptyAsync("Center Admin", BuildCenterAdminRules());
            await SeedRoleRulesIfEmptyAsync("Country Admin", BuildCountryAdminRules());
        }

        private async Task EnsureRoleExistsAsync(string roleName)
        {
            var existingRole = await _context.Roles
                .Include(r => r.NameTranslation)
                .FirstOrDefaultAsync(r => r.NameTranslation != null && r.NameTranslation.EN == roleName);

            if (existingRole != null)
                return;

            var translation = new Translation { EN = roleName };
            _context.Translations.Add(translation);
            await _context.SaveChangesAsync();

            _context.Roles.Add(new Role { NameTranslationId = translation.Id });
            await _context.SaveChangesAsync();
            _logger.LogInformation("{RoleName} role created.", roleName);
        }

        private async Task SeedSysAdminRulesAsync()
        {
            var adminRole = await GetRoleByNameAsync("SysAdmin");
            if (adminRole == null)
                return;

            var hasWildcardRule = await _context.RolePermissionRules
                .AnyAsync(r => r.RoleId == adminRole.Id && r.PermissionKey == "*");

            if (hasWildcardRule)
                return;

            _context.RolePermissionRules.Add(new RolePermissionRule
            {
                RoleId = adminRole.Id,
                PermissionKey = "*",
                Effect = AccessRuleEffect.Allow
            });

            _logger.LogInformation("Seeded SysAdmin wildcard permission rule.");
        }

        private async Task SeedRoleRulesIfEmptyAsync(string roleName, List<RolePermissionRule> rules)
        {
            var role = await GetRoleByNameAsync(roleName);
            if (role == null)
                return;

            var existingRules = await _context.RolePermissionRules
                .Where(r => r.RoleId == role.Id)
                .Include(r => r.Scopes)
                .ToListAsync();

            static string BuildScopeSignature(IEnumerable<RolePermissionScope> scopes)
            {
                return string.Join("|", scopes
                    .OrderBy(scope => scope.ResourceType)
                    .ThenBy(scope => scope.ResourceId)
                    .Select(scope => $"{scope.ResourceType}:{scope.ResourceId}"));
            }

            foreach (var rule in rules)
            {
                var incomingSignature = BuildScopeSignature(rule.Scopes);
                var exists = existingRules.Any(existing =>
                    string.Equals(existing.PermissionKey, rule.PermissionKey, StringComparison.OrdinalIgnoreCase) &&
                    existing.Effect == rule.Effect &&
                    BuildScopeSignature(existing.Scopes) == incomingSignature);

                if (exists)
                    continue;

                rule.RoleId = role.Id;
                _context.RolePermissionRules.Add(rule);
            }

            _logger.LogInformation("Ensured default ACL rules for role {RoleName}.", roleName);
        }

        private async Task<Role?> GetRoleByNameAsync(string roleName)
        {
            return await _context.Roles
                .Include(r => r.NameTranslation)
                .FirstOrDefaultAsync(r => r.NameTranslation != null && r.NameTranslation.EN == roleName);
        }

        private static List<RolePermissionRule> BuildExaminerRules()
        {
            return new List<RolePermissionRule>
            {
                Allow("POST:/api/auth/change-password"),
                Allow("POST:/api/auth/create-employee"),
                Allow("GET:/api/auth/next-uid"),
                Allow("GET:/api/admin/employees"),
                Allow("GET:/api/admin/employees/{employeeid}"),
                Allow("GET:/api/admin/roles"),
                Allow("GET:/api/employees/me"),
                Allow("GET:/api/employees/dashboard-stats"),
                Allow("GET:/api/events/options"),
                Allow("POST:/api/events*"),
                Allow("GET:/api/patients/search"),
                Allow("POST:/api/patients"),
                Allow("GET:/api/patients/{id}"),
                Allow("GET:/api/patients/{id}/detail"),
                Allow("POST:/api/patients/{id}/photo"),
                Allow("POST:/api/patients/{id}/documents"),
                Allow("GET:/api/patients/{id}/documents"),
                Allow("POST:/api/examinations/{examinationid}/documents"),
                Allow("GET:/api/examinations"),
                Allow("GET:/api/examinations/export")
            };
        }

        private static List<RolePermissionRule> BuildDoctorRules()
        {
            return new List<RolePermissionRule>
            {
                Allow("POST:/api/auth/change-password"),
                Allow("GET:/api/employees/me"),
                Allow("GET:/api/employees/dashboard-stats"),
                Allow("GET:/api/patients/search"),
                Allow("GET:/api/patients/{id}"),
                Allow("GET:/api/patients/{id}/detail"),
                Allow("GET:/api/patients/{id}/documents"),
                Allow("GET:/api/patients/{patientid}/documents/{documentid}"),
                Allow("GET:/api/examinations/{examinationid}/documents/{documentid}"),
                Allow("GET:/api/hospitals"),
                Allow("GET:/api/examinations"),
                Allow("GET:/api/examinations/export")
            };
        }

        private static List<RolePermissionRule> BuildCenterAdminRules()
        {
            return new List<RolePermissionRule>
            {
                Allow("POST:/api/auth/change-password"),
                Allow("GET:/api/employees/me"),
                Allow("GET:/api/employees/dashboard-stats"),
                Allow("GET:/api/hospitals", Scope("Hospital", 0)),
                Allow("GET:/api/hospitals/{hospitalid}/examination-types", Scope("Hospital", 0)),
                Allow("PUT:/api/hospitals/{hospitalid}", Scope("Hospital", 0)),
                Allow("GET:/api/examinationrooms/hospital/{hospitalid}", Scope("Hospital", 0)),
                Allow("POST:/api/examinationrooms", Scope("Hospital", 0)),
                Allow("PUT:/api/examinationrooms/{roomid}", Scope("Hospital", 0)),
                Allow("DELETE:/api/examinationrooms/{roomid}", Scope("Hospital", 0)),
                Allow("GET:/api/reservations/slots/hospital/{hospitalid}", Scope("Hospital", 0)),
                Allow("POST:/api/reservations/slots", Scope("Hospital", 0)),
                Allow("POST:/api/reservations/slots/hospital/{hospitalid}/copy-day", Scope("Hospital", 0)),
                Allow("PUT:/api/reservations/slots/{slotid}", Scope("Hospital", 0)),
                Allow("POST:/api/reservations/slots/{slotid}/release", Scope("Hospital", 0)),
                Allow("DELETE:/api/reservations/slots/{slotid}", Scope("Hospital", 0)),
                Allow("POST:/api/reservations/slots/{slotid}/block", Scope("Hospital", 0)),
                Allow("POST:/api/reservations/slots/{slotid}/confirm", Scope("Hospital", 0)),
                Allow("POST:/api/reservations/slots/{slotid}/reject", Scope("Hospital", 0)),
                Allow("GET:/api/examinations"),
                Allow("GET:/api/examinations/export")
            };
        }

        private static List<RolePermissionRule> BuildCountryAdminRules()
        {
            return new List<RolePermissionRule>
            {
                Allow("POST:/api/auth/change-password"),
                Allow("POST:/api/auth/create-employee"),
                Allow("GET:/api/auth/next-uid"),
                Allow("GET:/api/employees/me"),
                Allow("GET:/api/employees/dashboard-stats"),
                Allow("GET:/api/hospitals"),
                Allow("POST:/api/hospitals", Scope("Country", 0)),
                Allow("GET:/api/hospitals/{hospitalid}/examination-types", Scope("Country", 0)),
                Allow("PUT:/api/hospitals/{hospitalid}/examination-types", Scope("Country", 0)),
                Allow("PUT:/api/hospitals/{hospitalid}", Scope("Country", 0)),
                Allow("DELETE:/api/hospitals/{hospitalid}", Scope("Country", 0)),
                Allow("GET:/api/examinationrooms/hospital/{hospitalid}", Scope("Country", 0)),
                Allow("POST:/api/examinationrooms", Scope("Country", 0)),
                Allow("PUT:/api/examinationrooms/{roomid}", Scope("Country", 0)),
                Allow("DELETE:/api/examinationrooms/{roomid}", Scope("Country", 0)),
                Allow("GET:/api/admin/employees"),
                Allow("GET:/api/admin/employees/{employeeid}"),
                Allow("PUT:/api/admin/employees/{employeeid}"),
                Allow("PATCH:/api/admin/employees/{employeeid}/deactivate"),
                Allow("GET:/api/admin/roles"),
                Allow("GET:/api/examinationtypes"),
                Allow("POST:/api/doctorexaminationrooms", Scope("Country", 0)),
                Allow("DELETE:/api/doctorexaminationrooms/{assignmentid}", Scope("Country", 0)),
                Allow("GET:/api/doctorexaminationrooms/doctor/{doctorid}", Scope("Country", 0)),
                Allow("GET:/api/reservations/slots/hospital/{hospitalid}", Scope("Country", 0)),
                Allow("POST:/api/reservations/slots", Scope("Country", 0)),
                Allow("POST:/api/reservations/slots/hospital/{hospitalid}/copy-day", Scope("Country", 0)),
                Allow("PUT:/api/reservations/slots/{slotid}", Scope("Country", 0)),
                Allow("POST:/api/reservations/slots/{slotid}/release", Scope("Country", 0)),
                Allow("DELETE:/api/reservations/slots/{slotid}", Scope("Country", 0)),
                Allow("POST:/api/reservations/slots/{slotid}/block", Scope("Country", 0)),
                Allow("POST:/api/reservations/slots/{slotid}/confirm", Scope("Country", 0)),
                Allow("POST:/api/reservations/slots/{slotid}/reject", Scope("Country", 0)),
                Allow("GET:/api/examinations"),
                Allow("GET:/api/examinations/export")
            };
        }

        private static RolePermissionRule Allow(string permissionKey, params RolePermissionScope[] scopes)
        {
            return new RolePermissionRule
            {
                PermissionKey = permissionKey,
                Effect = AccessRuleEffect.Allow,
                Scopes = scopes.ToList()
            };
        }

        private static RolePermissionScope Scope(string resourceType, int resourceId)
        {
            return new RolePermissionScope
            {
                ResourceType = resourceType,
                ResourceId = resourceId
            };
        }
    }
}
