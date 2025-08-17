namespace HealthcareAPI.Models
{
    public class CreateEmployeeResponse
    {
        public int EmployeeId { get; set; }
        public int PersonId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UID { get; set; } = string.Empty;
        public bool Active { get; set; }
    }
}
