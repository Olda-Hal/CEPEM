namespace HealthcareAPI.Models
{
    public class ActivityLog
    {
        public DateTime Timestamp { get; set; }
        public string Service { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;

        // Who performed the action (from the validated JWT), when known
        public int? EmployeeId { get; set; }
        public string? EmployeeEmail { get; set; }

        // HTTP method and response status for the logged request
        public string? Method { get; set; }
        public int? StatusCode { get; set; }
    }
}
