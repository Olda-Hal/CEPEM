using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class MedicalRecord
{
    public int Id { get; set; }
    
    [Required]
    public string Diagnosis { get; set; } = string.Empty;
    
    public string? Treatment { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime VisitDate { get; set; } = DateTime.UtcNow;
    
    [Required]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    
    [Required]
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
}
