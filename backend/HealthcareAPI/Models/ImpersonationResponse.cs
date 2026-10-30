namespace HealthcareAPI.Models
{
    /// <summary>
    /// Result of a SysAdmin "preview as employee" request: a short-lived JWT
    /// carrying the target employee's identity plus the impersonator's id.
    /// </summary>
    public class ImpersonationResponse
    {
        public string Token { get; set; } = string.Empty;
        public int EmployeeId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public string ImpersonatorName { get; set; } = string.Empty;
    }
}
