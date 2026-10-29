using HealthcareAPI.Models;

namespace HealthcareAPI.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
        Task<bool> ChangePasswordAsync(int employeeId, ChangePasswordRequest request);
        Task<(CreateEmployeeResponse? Result, string? ErrorBody)> CreateEmployeeAsync(CreateEmployeeRequest request);
        Task<string> GetNextAvailableUidAsync();
    }
}
