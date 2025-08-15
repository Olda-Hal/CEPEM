using HealthcareAPI.Models;

namespace HealthcareAPI.Repositories
{
    public interface IDoctorRepository
    {
        Task<Doctor?> GetByIdAsync(int id);
        Task<Doctor?> GetByEmailAsync(string email);
        Task<int> GetActiveDoctorsCountAsync();
        Task<int> GetTotalCountAsync();
        Task UpdateLastLoginAsync(int doctorId, DateTime lastLoginAt);
    }
}
