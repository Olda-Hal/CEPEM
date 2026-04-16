namespace DatabaseAPI.DatabaseModels;

public class FormSubmissionLifestyle
{
    public int Id { get; set; }

    public int FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;

    public bool PoorSleep { get; set; }
    public bool DigestiveIssues { get; set; }
    public bool PhysicalStress { get; set; }
    public bool MentalStress { get; set; }
    public bool Smoking { get; set; }
    public bool Fatigue { get; set; }

    public float? LastMealHours { get; set; }

    public bool VaccinesAfter2023 { get; set; }

    public string? AdditionalHealthInfo { get; set; }
}
