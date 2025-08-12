using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class Prescription
{
    public int Id { get; set; }
    
    [Required]
    public string MedicationName { get; set; } = string.Empty;
    
    [Required]
    public string Dosage { get; set; } = string.Empty;
    
    public string? Instructions { get; set; }
    
    public DateTime PrescribedDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? ExpiryDate { get; set; }
    
    [Required]
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    
    [Required]
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public bool IsActive { get; set; } = true;
}
