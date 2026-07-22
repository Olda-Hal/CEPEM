using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.APIModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services
{
    public interface IEmployeeManagementService
    {
        Task<List<EmployeeListItem>> GetAllEmployeesAsync();
        Task<EmployeeListItem?> GetEmployeeByIdAsync(int employeeId);
        Task<UpdateEmployeeResponse> UpdateEmployeeAsync(int employeeId, UpdateEmployeeRequest request);
        Task<bool> DeactivateEmployeeAsync(int employeeId);
        Task<List<RoleDto>> GetAllRolesAsync();
        Task<RoleDto?> CreateRoleAsync(CreateRoleRequest request);
    }

    public class EmployeeManagementService : IEmployeeManagementService
    {
        private readonly DatabaseContext _context;

        public EmployeeManagementService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<List<EmployeeListItem>> GetAllEmployeesAsync()
        {
            var employees = await _context.Employees
                .Include(e => e.Person)
                    .ThenInclude(p => p.UserRoles)
                        .ThenInclude(ur => ur.Role)
                            .ThenInclude(r => r.NameTranslation)
                .Include(e => e.Person)
                    .ThenInclude(p => p.ContactToObjects)
                        .ThenInclude(cto => cto.Contact)
                            .ThenInclude(c => c.Emails)
                .Include(e => e.Person)
                    .ThenInclude(p => p.ContactToObjects)
                        .ThenInclude(cto => cto.Contact)
                            .ThenInclude(c => c.PhoneNumbers)
                .Select(e => new EmployeeListItem
                {
                    EmployeeId = e.Id,
                    CreatedByEmployeeId = e.CreatedByEmployeeId,
                    PersonId = e.PersonId,
                    FirstName = e.Person.FirstName,
                    LastName = e.Person.LastName,
                    Email = _context.ContactToObjects
                        .Where(cto => cto.ObjectType == ContactObjectType.Person && cto.ObjectId == e.PersonId)
                        .Join(_context.ContactEmails, cto => cto.ContactId, ce => ce.ContactId, (_, ce) => ce.Email)
                        .FirstOrDefault() ?? string.Empty,
                    PhoneNumber = _context.ContactToObjects
                        .Where(cto => cto.ObjectType == ContactObjectType.Person && cto.ObjectId == e.PersonId)
                        .Join(_context.ContactPhoneNumbers, cto => cto.ContactId, cp => cp.ContactId, (_, cp) => cp.PhoneNumber)
                        .FirstOrDefault() ?? string.Empty,
                    UID = e.Person.UID,
                    Gender = e.Person.Gender,
                    TitleBefore = e.Person.TitleBefore,
                    TitleAfter = e.Person.TitleAfter,
                    Active = e.Person.Active,
                    LastLoginAt = e.LastLoginAt,
                    PasswordExpiration = e.PasswordExpiration,
                    Roles = e.Person.UserRoles.Select(ur => ur.Role.NameTranslation != null ? ur.Role.NameTranslation.EN : string.Empty).ToList(),
                    FullName = $"{e.Person.TitleBefore} {e.Person.FirstName} {e.Person.LastName} {e.Person.TitleAfter}".Trim()
                })
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();

            return employees;
        }

        public async Task<EmployeeListItem?> GetEmployeeByIdAsync(int employeeId)
        {
            var employee = await _context.Employees
                .Include(e => e.Person)
                    .ThenInclude(p => p.UserRoles)
                        .ThenInclude(ur => ur.Role)
                            .ThenInclude(r => r.NameTranslation)
                .Include(e => e.Person)
                    .ThenInclude(p => p.ContactToObjects)
                        .ThenInclude(cto => cto.Contact)
                            .ThenInclude(c => c.Emails)
                .Include(e => e.Person)
                    .ThenInclude(p => p.ContactToObjects)
                        .ThenInclude(cto => cto.Contact)
                            .ThenInclude(c => c.PhoneNumbers)
                .Where(e => e.Id == employeeId)
                .Select(e => new EmployeeListItem
                {
                    EmployeeId = e.Id,
                    CreatedByEmployeeId = e.CreatedByEmployeeId,
                    PersonId = e.PersonId,
                    FirstName = e.Person.FirstName,
                    LastName = e.Person.LastName,
                    Email = _context.ContactToObjects
                        .Where(cto => cto.ObjectType == ContactObjectType.Person && cto.ObjectId == e.PersonId)
                        .Join(_context.ContactEmails, cto => cto.ContactId, ce => ce.ContactId, (_, ce) => ce.Email)
                        .FirstOrDefault() ?? string.Empty,
                    PhoneNumber = _context.ContactToObjects
                        .Where(cto => cto.ObjectType == ContactObjectType.Person && cto.ObjectId == e.PersonId)
                        .Join(_context.ContactPhoneNumbers, cto => cto.ContactId, cp => cp.ContactId, (_, cp) => cp.PhoneNumber)
                        .FirstOrDefault() ?? string.Empty,
                    UID = e.Person.UID,
                    Gender = e.Person.Gender,
                    TitleBefore = e.Person.TitleBefore,
                    TitleAfter = e.Person.TitleAfter,
                    Active = e.Person.Active,
                    LastLoginAt = e.LastLoginAt,
                    PasswordExpiration = e.PasswordExpiration,
                    Roles = e.Person.UserRoles.Select(ur => ur.Role.NameTranslation != null ? ur.Role.NameTranslation.EN : string.Empty).ToList(),
                    FullName = $"{e.Person.TitleBefore} {e.Person.FirstName} {e.Person.LastName} {e.Person.TitleAfter}".Trim()
                })
                .FirstOrDefaultAsync();

            return employee;
        }

        public async Task<UpdateEmployeeResponse> UpdateEmployeeAsync(int employeeId, UpdateEmployeeRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = await _context.Employees
                    .Include(e => e.Person)
                        .ThenInclude(p => p.UserRoles)
                    .Include(e => e.Person)
                        .ThenInclude(p => p.ContactToObjects)
                            .ThenInclude(cto => cto.Contact)
                                .ThenInclude(c => c.Emails)
                    .Include(e => e.Person)
                        .ThenInclude(p => p.ContactToObjects)
                            .ThenInclude(cto => cto.Contact)
                                .ThenInclude(c => c.PhoneNumbers)
                    .FirstOrDefaultAsync(e => e.Id == employeeId);

                if (employee == null)
                {
                    return new UpdateEmployeeResponse
                    {
                        Success = false,
                        Message = "Employee not found"
                    };
                }

                // Check if UID already exists for other persons
                var normalizedEmail = request.Email.Trim();
                request.Email = normalizedEmail;

                var uidExists = await _context.Persons
                    .AnyAsync(p => p.Id != employee.PersonId && p.UID == request.UID);

                var emailExists = await _context.ContactToObjects
                    .Where(cto => cto.ObjectType == ContactObjectType.Person && cto.ObjectId != employee.PersonId)
                    .Join(
                        _context.ContactEmails,
                        cto => cto.ContactId,
                        ce => ce.ContactId,
                        (_, ce) => ce.Email)
                    .AnyAsync(email => email == normalizedEmail);

                if (uidExists || emailExists)
                {
                    return new UpdateEmployeeResponse
                    {
                        Success = false,
                        Message = "Email or UID already exists for another person"
                    };
                }

                // Update person data
                employee.Person.FirstName = request.FirstName;
                employee.Person.LastName = request.LastName;
                employee.Person.UID = request.UID;
                employee.Person.Gender = request.Gender;
                employee.Person.TitleBefore = request.TitleBefore;
                employee.Person.TitleAfter = request.TitleAfter;
                employee.Person.Active = request.Active;

                // Update Contact email and phone via ContactToObjects
                var personContact = employee.Person.ContactToObjects.FirstOrDefault()
                    ?? await _context.ContactToObjects
                        .Include(cto => cto.Contact)
                            .ThenInclude(c => c.Emails)
                        .Include(cto => cto.Contact)
                            .ThenInclude(c => c.PhoneNumbers)
                        .FirstOrDefaultAsync(cto => cto.ObjectType == ContactObjectType.Person && cto.ObjectId == employee.PersonId);
                if (personContact == null)
                {
                    var contact = new Contact();
                    _context.Contacts.Add(contact);
                    await _context.SaveChangesAsync();
                    _context.ContactToObjects.Add(new ContactToObject
                    {
                        ContactId = contact.Id,
                        ObjectId = employee.PersonId,
                        ObjectType = ContactObjectType.Person,
                        PersonId = employee.PersonId
                    });
                    await _context.SaveChangesAsync();

                    _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = request.Email });
                    _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = contact.Id, PhoneNumber = request.PhoneNumber });
                }
                else
                {
                    var existingEmail = personContact.Contact.Emails.FirstOrDefault();
                    if (existingEmail != null) existingEmail.Email = request.Email;
                    else _context.ContactEmails.Add(new ContactEmail { ContactId = personContact.ContactId, Email = request.Email });

                    var existingPhone = personContact.Contact.PhoneNumbers.FirstOrDefault();
                    if (existingPhone != null) existingPhone.PhoneNumber = request.PhoneNumber;
                    else _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = personContact.ContactId, PhoneNumber = request.PhoneNumber });
                }

                // Update roles
                // Remove existing roles
                var existingRoles = employee.Person.UserRoles.ToList();
                _context.UserRoles.RemoveRange(existingRoles);

                // Add new roles
                foreach (var roleId in request.RoleIds)
                {
                    var roleExists = await _context.Roles.AnyAsync(r => r.Id == roleId);
                    if (roleExists)
                    {
                        employee.Person.UserRoles.Add(new UserRole
                        {
                            UserId = employee.PersonId,
                            RoleId = roleId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Get updated employee data
                var updatedEmployee = await GetEmployeeByIdAsync(employeeId);

                return new UpdateEmployeeResponse
                {
                    Success = true,
                    Message = "Employee updated successfully",
                    Employee = updatedEmployee
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new UpdateEmployeeResponse
                {
                    Success = false,
                    Message = $"Error updating employee: {ex.Message}"
                };
            }
        }

        public async Task<bool> DeactivateEmployeeAsync(int employeeId)
        {
            try
            {
                var employee = await _context.Employees
                    .Include(e => e.Person)
                    .FirstOrDefaultAsync(e => e.Id == employeeId);

                if (employee == null)
                {
                    return false;
                }

                employee.Person.Active = false;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<RoleDto>> GetAllRolesAsync()
        {
            var roles = await _context.Roles
                .Include(r => r.NameTranslation)
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.NameTranslation != null ? r.NameTranslation.EN : string.Empty
                })
                .OrderBy(r => r.Name)
                .ToListAsync();

            return roles;
        }

        public async Task<RoleDto?> CreateRoleAsync(CreateRoleRequest request)
        {
            var roleName = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return null;
            }

            var exists = await _context.Roles
                .Include(r => r.NameTranslation)
                .AnyAsync(r => r.NameTranslation != null && r.NameTranslation.EN == roleName);

            if (exists)
            {
                return null;
            }

            var translation = new Translation
            {
                EN = roleName,
                CS = roleName
            };
            _context.Translations.Add(translation);
            await _context.SaveChangesAsync();

            var role = new Role
            {
                NameTranslationId = translation.Id
            };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            return new RoleDto
            {
                Id = role.Id,
                Name = roleName
            };
        }
    }
}
