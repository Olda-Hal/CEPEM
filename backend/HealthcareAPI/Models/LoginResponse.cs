namespace HealthcareAPI.Models
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
    }
}
