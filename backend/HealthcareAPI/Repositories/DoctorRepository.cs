using Microsoft.EntityFrameworkCore;
using HealthcareAPI.Data;
using HealthcareAPI.Models;

namespace HealthcareAPI.Repositories
{
    public class DoctorRepository : IDoctorRepository
    {
        private readonly HealthcareDbContext _context;

        public DoctorRepository(HealthcareDbContext context)
        {
            _context = context;
        }

        public async Task<Doctor?> GetByIdAsync(int id)
        {
            return await _context.Doctors
                .Where(d => d.Id == id && d.IsActive)
                .Select(d => new Doctor
                {
                    Id = d.Id,
                    FirstName = d.FirstName,
                    LastName = d.LastName,
                    Email = d.Email,
                    Specialization = d.Specialization,
                    LicenseNumber = d.LicenseNumber,
                    IsActive = d.IsActive,
                    CreatedAt = d.CreatedAt,
                    LastLoginAt = d.LastLoginAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<Doctor?> GetByEmailAsync(string email)
        {
            return await _context.Doctors
                .FirstOrDefaultAsync(d => d.Email == email && d.IsActive);
        }

        public async Task<int> GetActiveDoctorsCountAsync()
        {
            return await _context.Doctors.CountAsync(d => d.IsActive);
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await _context.Doctors.CountAsync();
        }

        public async Task UpdateLastLoginAsync(int doctorId, DateTime lastLoginAt)
        {
            var doctor = await _context.Doctors.FindAsync(doctorId);
            if (doctor != null)
            {
                doctor.LastLoginAt = lastLoginAt;
                await _context.SaveChangesAsync();
            }
        }
    }
}
