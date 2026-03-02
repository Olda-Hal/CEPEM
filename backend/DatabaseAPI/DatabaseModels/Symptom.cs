namespace DatabaseAPI.DatabaseModels;

public class Symptom
{
    public int Id { get; set; }

    public int? NameTranslationId { get; set; }
    public Translation? NameTranslation { get; set; }

    // Navigation properties
    public ICollection<PatientSymptom> PatientSymptoms { get; set; } = new List<PatientSymptom>();
}
