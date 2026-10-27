using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class ActivityLog
{
    public int Id { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [Required]
    public string Service { get; set; } = string.Empty;
    
    [Required]
    public string Action { get; set; } = string.Empty;
    
    public string? Details { get; set; }

    // Who performed the action (from the validated JWT), when known
    public int? EmployeeId { get; set; }
    public string? EmployeeEmail { get; set; }

    // HTTP method and response status for the logged request
    public string? Method { get; set; }
    public int? StatusCode { get; set; }
}
