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
                .Select(e => new EmployeeListItem
                {
                    EmployeeId = e.Id,
                    PersonId = e.PersonId,
                    FirstName = e.Person.FirstName,
                    LastName = e.Person.LastName,
                    Email = e.Person.Email,
                    PhoneNumber = e.Person.PhoneNumber,
                    UID = e.Person.UID,
                    Gender = e.Person.Gender,
                    TitleBefore = e.Person.TitleBefore,
                    TitleAfter = e.Person.TitleAfter,
                    Active = e.Person.Active,
                    LastLoginAt = e.LastLoginAt,
                    PasswordExpiration = e.PasswordExpiration,
                    Roles = e.Person.UserRoles.Select(ur => ur.Role.Name).ToList(),
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
                .Where(e => e.Id == employeeId)
                .Select(e => new EmployeeListItem
                {
                    EmployeeId = e.Id,
                    PersonId = e.PersonId,
                    FirstName = e.Person.FirstName,
                    LastName = e.Person.LastName,
                    Email = e.Person.Email,
                    PhoneNumber = e.Person.PhoneNumber,
                    UID = e.Person.UID,
                    Gender = e.Person.Gender,
                    TitleBefore = e.Person.TitleBefore,
                    TitleAfter = e.Person.TitleAfter,
                    Active = e.Person.Active,
                    LastLoginAt = e.LastLoginAt,
                    PasswordExpiration = e.PasswordExpiration,
                    Roles = e.Person.UserRoles.Select(ur => ur.Role.Name).ToList(),
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
                    .FirstOrDefaultAsync(e => e.Id == employeeId);

                if (employee == null)
                {
                    return new UpdateEmployeeResponse
                    {
                        Success = false,
                        Message = "Employee not found"
                    };
                }

                // Check if email or UID already exists for other persons
                var existingPerson = await _context.Persons
                    .FirstOrDefaultAsync(p => p.Id != employee.PersonId && 
                                           (p.Email == request.Email || p.UID == request.UID));

                if (existingPerson != null)
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
                employee.Person.Email = request.Email;
                employee.Person.PhoneNumber = request.PhoneNumber;
                employee.Person.UID = request.UID;
                employee.Person.Gender = request.Gender;
                employee.Person.TitleBefore = request.TitleBefore;
                employee.Person.TitleAfter = request.TitleAfter;
                employee.Person.Active = request.Active;

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
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name
                })
                .OrderBy(r => r.Name)
                .ToListAsync();

            return roles;
        }
    }
}
