using HealthcareAPI.Models;

namespace HealthcareAPI.Services
{
    public interface IDoctorService
    {
        Task<Doctor?> GetCurrentDoctorAsync(int doctorId);
        Task<object> GetDashboardStatsAsync(int doctorId);
    }
}
