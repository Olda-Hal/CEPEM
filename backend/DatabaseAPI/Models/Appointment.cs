using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class Appointment
{
    public int Id { get; set; }
    
    [Required]
    public string Title { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public DateTime ScheduledDateTime { get; set; }
    
    public int? DurationMinutes { get; set; } = 30;
    
    [Required]
    public string Status { get; set; } = "Scheduled"; // Scheduled, Completed, Cancelled, NoShow
    
    [Required]
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    
    [Required]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
}
