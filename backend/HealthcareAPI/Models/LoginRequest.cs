namespace HealthcareAPI.Models
{
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public enum AuthenticationResult
    {
        Success,
        InvalidCredentials,
        AccountDeactivated,
        UserNotFound
    }

    public class AuthenticationResponse
    {
        public AuthenticationResult Result { get; set; }
        public string Message { get; set; } = string.Empty;
        public EmployeeAuthInfo? EmployeeInfo { get; set; }
    }
}
