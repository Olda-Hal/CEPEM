namespace HealthcareAPI.Models
{
    public class CreateEmployeeRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string UID { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? TitleBefore { get; set; }
        public string? TitleAfter { get; set; }
        public bool Active { get; set; } = true;

        /// <summary>Roles for the new account; empty = baseline Examiner.</summary>
        public List<int> RoleIds { get; set; } = new List<int>();

        /// <summary>Working country of the employee (e.g. CZ); empty = creator's country.</summary>
        public string? CountryCode { get; set; }

        /// <summary>Centers the employee is assigned to.</summary>
        public List<int> HospitalIds { get; set; } = new List<int>();
    }
}
