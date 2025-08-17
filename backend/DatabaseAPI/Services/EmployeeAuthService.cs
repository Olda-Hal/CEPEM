using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.APIModels;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DatabaseAPI.Services
{
    public interface IEmployeeAuthService
    {
        Task<EmployeeAuthInfo?> AuthenticateAsync(string email, string password);
        Task<AuthenticationResponse> AuthenticateDetailedAsync(string email, string password);
        Task UpdateLastLoginAsync(int employeeId);
        Task<bool> ChangePasswordAsync(int employeeId, string currentPassword, string newPassword);
        Task<PasswordChangeResponse> ChangePasswordDetailedAsync(int employeeId, string currentPassword, string newPassword);
        Task<CreateEmployeeResponse?> CreateEmployeeAsync(CreateEmployeeRequest request);
        Task<string> GetNextAvailableUidAsync();
    }

    public class EmployeeAuthService : IEmployeeAuthService
    {
        private readonly DatabaseContext _context;

        public EmployeeAuthService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<EmployeeAuthInfo?> AuthenticateAsync(string email, string password)
        {
            var result = await AuthenticateDetailedAsync(email, password);
            return result.Result == AuthenticationResult.Success ? result.EmployeeInfo : null;
        }

        public async Task<AuthenticationResponse> AuthenticateDetailedAsync(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                return new AuthenticationResponse
                {
                    Result = AuthenticationResult.InvalidCredentials,
                    Message = "Email and password are required"
                };
            }

            // Najdi zaměstnance podle emailu bez ohledu na aktivitu
            var employeeInfo = await _context.Employees
                .Include(e => e.Person)
                .ThenInclude(p => p.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(e => e.Person.Email == email)
                .Select(e => new EmployeeAuthInfo
                {
                    EmployeeId = e.Id,
                    PersonId = e.PersonId,
                    FirstName = e.Person.FirstName,
                    LastName = e.Person.LastName,
                    Email = e.Person.Email,
                    PasswordHash = e.PasswordHash,
                    Salt = e.Salt,
                    Active = e.Person.Active,
                    UID = e.Person.UID,
                    TitleBefore = e.Person.TitleBefore,
                    TitleAfter = e.Person.TitleAfter,
                    LastLoginAt = e.LastLoginAt,
                    PasswordExpiration = e.PasswordExpiration == DateTime.MinValue ? null : e.PasswordExpiration,
                    Roles = e.Person.UserRoles.Select(ur => ur.Role.Name).ToList()
                })
                .FirstOrDefaultAsync();

            if (employeeInfo == null)
            {
                return new AuthenticationResponse
                {
                    Result = AuthenticationResult.UserNotFound,
                    Message = "User not found"
                };
            }

            // Ověř heslo
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, employeeInfo.PasswordHash);
            if (!isPasswordValid)
            {
                return new AuthenticationResponse
                {
                    Result = AuthenticationResult.InvalidCredentials,
                    Message = "Invalid credentials"
                };
            }

            // Zkontroluj, jestli je účet aktivní
            if (!employeeInfo.Active)
            {
                return new AuthenticationResponse
                {
                    Result = AuthenticationResult.AccountDeactivated,
                    Message = "Account has been deactivated"
                };
            }

            return new AuthenticationResponse
            {
                Result = AuthenticationResult.Success,
                Message = "Authentication successful",
                EmployeeInfo = employeeInfo
            };
        }

        public async Task UpdateLastLoginAsync(int employeeId)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee != null)
            {
                employee.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ChangePasswordAsync(int employeeId, string currentPassword, string newPassword)
        {
            var employee = await _context.Employees
                .Include(e => e.Person)
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.Person.Active);
            
            if (employee == null)
            {
                return false;
            }

            // Verify current password
            bool isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(currentPassword, employee.PasswordHash);
            if (!isCurrentPasswordValid)
            {
                return false;
            }

            // Check if new password is the same as current password
            bool isSamePassword = BCrypt.Net.BCrypt.Verify(newPassword, employee.PasswordHash);
            if (isSamePassword)
            {
                return false;
            }

            // Generate new salt and hash
            string newSalt = BCrypt.Net.BCrypt.GenerateSalt();
            string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, newSalt);

            // Update password and extend expiration by 90 days
            employee.PasswordHash = newPasswordHash;
            employee.Salt = newSalt;
            employee.PasswordExpiration = DateTime.UtcNow.AddDays(90);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PasswordChangeResponse> ChangePasswordDetailedAsync(int employeeId, string currentPassword, string newPassword)
        {
            var employee = await _context.Employees
                .Include(e => e.Person)
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.Person.Active);
            
            if (employee == null)
            {
                return new PasswordChangeResponse 
                { 
                    Result = PasswordChangeResult.ValidationError, 
                    Message = "Employee not found" 
                };
            }

            // Verify current password
            bool isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(currentPassword, employee.PasswordHash);
            if (!isCurrentPasswordValid)
            {
                return new PasswordChangeResponse 
                { 
                    Result = PasswordChangeResult.InvalidCurrentPassword, 
                    Message = "Current password is incorrect" 
                };
            }

            // Check if new password is the same as current password
            bool isSamePassword = BCrypt.Net.BCrypt.Verify(newPassword, employee.PasswordHash);
            if (isSamePassword)
            {
                return new PasswordChangeResponse 
                { 
                    Result = PasswordChangeResult.SamePassword, 
                    Message = "New password cannot be the same as current password" 
                };
            }

            // Generate new salt and hash
            string newSalt = BCrypt.Net.BCrypt.GenerateSalt();
            string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, newSalt);

            // Update password and extend expiration by 90 days
            employee.PasswordHash = newPasswordHash;
            employee.Salt = newSalt;
            employee.PasswordExpiration = DateTime.UtcNow.AddDays(90);

            await _context.SaveChangesAsync();
            
            return new PasswordChangeResponse 
            { 
                Result = PasswordChangeResult.Success, 
                Message = "Password changed successfully" 
            };
        }

        public async Task<CreateEmployeeResponse?> CreateEmployeeAsync(CreateEmployeeRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Check if email or UID already exists
                var existingPerson = await _context.Persons
                    .FirstOrDefaultAsync(p => p.Email == request.Email || p.UID == request.UID);
                
                if (existingPerson != null)
                {
                    return null;
                }

                // Create new Person
                var person = new Person
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    UID = request.UID,
                    Gender = request.Gender,
                    TitleBefore = request.TitleBefore,
                    TitleAfter = request.TitleAfter,
                    Active = request.Active,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Persons.Add(person);
                await _context.SaveChangesAsync();

                // Generate password hash and salt
                string salt = BCrypt.Net.BCrypt.GenerateSalt();
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, salt);

                // Create new Employee
                var employee = new Employee
                {
                    PersonId = person.Id,
                    PasswordHash = passwordHash,
                    Salt = salt,
                    PasswordExpiration = DateTime.UtcNow.AddDays(-1) // Force password change on first login
                };

                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new CreateEmployeeResponse
                {
                    EmployeeId = employee.Id,
                    PersonId = person.Id,
                    Email = person.Email,
                    FullName = $"{person.TitleBefore} {person.FirstName} {person.LastName} {person.TitleAfter}".Trim(),
                    UID = person.UID,
                    Active = person.Active
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return null;
            }
        }

        public async Task<string> GetNextAvailableUidAsync()
        {
            try
            {
                // Get all existing UIDs from the database
                var existingUids = await _context.Persons
                    .Select(p => p.UID)
                    .Where(uid => !string.IsNullOrEmpty(uid))
                    .ToListAsync();

                // Convert to integers (assuming UIDs are numeric)
                var numericUids = existingUids
                    .Where(uid => int.TryParse(uid, out _))
                    .Select(int.Parse)
                    .OrderBy(x => x)
                    .ToList();

                // Find the first gap or return next sequential number
                int nextUid = 1;
                foreach (var uid in numericUids)
                {
                    if (uid == nextUid)
                    {
                        nextUid++;
                    }
                    else if (uid > nextUid)
                    {
                        break;
                    }
                }

                return nextUid.ToString();
            }
            catch (Exception)
            {
                // Fallback: return a timestamp-based UID
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            }
        }
    }
}
