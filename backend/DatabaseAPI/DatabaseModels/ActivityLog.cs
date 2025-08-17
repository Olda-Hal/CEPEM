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
}
