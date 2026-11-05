using HealthcareAPI.Models;

namespace HealthcareAPI.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);

        /// <summary>
        /// Exchange a valid refresh token for a new short-lived access token.
        /// Returns null when the refresh token is invalid, expired or revoked.
        /// </summary>
        Task<LoginResponse?> RefreshAsync(string refreshToken);

        /// <summary>
        /// Issue a short-lived token for the target employee so a SysAdmin can
        /// preview the system under that account. Returns null when the target
        /// does not exist or is deactivated.
        /// </summary>
        Task<ImpersonationResponse?> ImpersonateAsync(int targetEmployeeId, int impersonatorEmployeeId, string impersonatorName);

        Task<bool> ChangePasswordAsync(int employeeId, ChangePasswordRequest request);
        Task<(CreateEmployeeResponse? Result, string? ErrorBody)> CreateEmployeeAsync(CreateEmployeeRequest request);
        Task<string> GetNextAvailableUidAsync();
    }
}
