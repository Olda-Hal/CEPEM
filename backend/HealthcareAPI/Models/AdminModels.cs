namespace HealthcareAPI.Models
{
    public class EmployeeListItem
    {
        public int EmployeeId { get; set; }
        public int PersonId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string UID { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string? TitleBefore { get; set; }
        public string? TitleAfter { get; set; }
        public bool Active { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime PasswordExpiration { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public string FullName { get; set; } = string.Empty;
    }

    public class UpdateEmployeeRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string UID { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string? TitleBefore { get; set; }
        public string? TitleAfter { get; set; }
        public bool Active { get; set; }
        public List<int> RoleIds { get; set; } = new List<int>();
    }

    public class UpdateEmployeeResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public EmployeeListItem? Employee { get; set; }
    }

    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
