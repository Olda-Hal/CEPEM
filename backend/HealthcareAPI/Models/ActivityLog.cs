namespace HealthcareAPI.Models
{
    public class ActivityLog
    {
        public DateTime Timestamp { get; set; }
        public string Service { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
    }
}
