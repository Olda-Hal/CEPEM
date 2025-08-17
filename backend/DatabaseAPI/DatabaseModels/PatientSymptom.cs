using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class PatientSymptom
{
    public int Id { get; set; }
    
    [Required]
    public int SymptomId { get; set; }
    public Symptom Symptom { get; set; } = null!;
    
    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
}
