using HealthcareAPI.Models;
using HealthcareAPI.Repositories;

namespace HealthcareAPI.Services
{
    public class DoctorService : IDoctorService
    {
        private readonly IDoctorRepository _doctorRepository;

        public DoctorService(IDoctorRepository doctorRepository)
        {
            _doctorRepository = doctorRepository;
        }

        public async Task<Doctor?> GetCurrentDoctorAsync(int doctorId)
        {
            return await _doctorRepository.GetByIdAsync(doctorId);
        }

        public async Task<object> GetDashboardStatsAsync(int doctorId)
        {
            var doctor = await _doctorRepository.GetByIdAsync(doctorId);
            if (doctor == null)
            {
                return null;
            }

            var totalDoctors = await _doctorRepository.GetActiveDoctorsCountAsync();

            return new
            {
                TotalDoctors = totalDoctors,
                MySpecialization = doctor.Specialization,
                LastLogin = doctor.LastLoginAt,
                SystemStatus = "Online"
            };
        }
    }
}
