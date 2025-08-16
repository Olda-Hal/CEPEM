using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class Symptom
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<PatientSymptom> PatientSymptoms { get; set; } = new List<PatientSymptom>();
}
