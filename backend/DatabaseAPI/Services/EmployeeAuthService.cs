using DatabaseAPI.Data;
using DatabaseAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DatabaseAPI.Services
{
    public interface IEmployeeAuthService
    {
        Task<EmployeeAuthInfo?> AuthenticateAsync(string email, string password);
        Task UpdateLastLoginAsync(int employeeId);
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
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            // Najdi zaměstnance podle emailu z joinu Person a Employee tabulek
            var employeeInfo = await _context.Employees
                .Include(e => e.Person)
                .Where(e => e.Person.Email == email && e.Person.Active)
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
                    LastLoginAt = e.LastLoginAt
                })
                .FirstOrDefaultAsync();

            if (employeeInfo == null)
            {
                return null;
            }

            // Ověř heslo
            Debug.WriteLine("Verifying password for employee: " + employeeInfo.Email);
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, employeeInfo.PasswordHash);
            if (!isPasswordValid)
            {
                Debug.WriteLine("Invalid password for employee: " + employeeInfo.Email);
                return null;
            }

            Debug.WriteLine("Authentication successful for employee: " + employeeInfo.Email);
            return employeeInfo;
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
    }
}
