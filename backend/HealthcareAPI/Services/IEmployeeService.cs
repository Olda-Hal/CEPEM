using HealthcareAPI.Models;

namespace HealthcareAPI.Services
{
    public interface IEmployeeService
    {
        Task<Employee?> GetCurrentEmployeeAsync(int employeeId);
        Task<object> GetDashboardStatsAsync(int employeeId);
    }
}
