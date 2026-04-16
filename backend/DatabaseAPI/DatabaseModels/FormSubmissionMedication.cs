namespace DatabaseAPI.DatabaseModels;

public class FormSubmissionMedication
{
    public int Id { get; set; }

    public int FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;

    public bool MedBloodPressure { get; set; }
    public bool MedHeart { get; set; }
    public bool MedCholesterol { get; set; }
    public bool MedBloodThinners { get; set; }
    public bool MedDiabetes { get; set; }
    public bool MedThyroid { get; set; }
    public bool MedNerves { get; set; }
    public bool MedPsych { get; set; }
    public bool MedDigestion { get; set; }
    public bool MedPain { get; set; }
    public bool MedDehydration { get; set; }
    public bool MedBreathing { get; set; }
    public bool MedAntibiotics { get; set; }
    public bool MedSupplements { get; set; }
    public bool MedAllergies { get; set; }
}
