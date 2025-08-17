using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Event
{
    public int Id { get; set; }
    
    [Required]
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    
    [Required]
    public int EventTypeId { get; set; }
    public EventType EventType { get; set; } = null!;
    
    public DateTime HappenedAt { get; set; }
    
    public DateTime? HappenedTo { get; set; }
    
    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }
    
    // Navigation properties
    public ICollection<DrugUse> DrugUses { get; set; } = new List<DrugUse>();
    public ICollection<Examination> Examinations { get; set; } = new List<Examination>();
    public ICollection<PatientSymptom> PatientSymptoms { get; set; } = new List<PatientSymptom>();
    public ICollection<Pregnancy> Pregnancies { get; set; } = new List<Pregnancy>();
    public ICollection<Injury> Injuries { get; set; } = new List<Injury>();
    public ICollection<Vaccine> Vaccines { get; set; } = new List<Vaccine>();
}
