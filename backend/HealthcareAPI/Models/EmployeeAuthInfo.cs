namespace HealthcareAPI.Models
{
    public class EmployeeAuthInfo
    {
        public int EmployeeId { get; set; }
        public int PersonId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public bool Active { get; set; }
        public string UID { get; set; } = string.Empty;
        public string? TitleBefore { get; set; }
        public string? TitleAfter { get; set; }
        public DateTime? LastLoginAt { get; set; }
        
        public string FullName => $"{TitleBefore} {FirstName} {LastName} {TitleAfter}".Trim();
    }
}
