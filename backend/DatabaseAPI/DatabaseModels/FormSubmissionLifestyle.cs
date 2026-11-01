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

    public bool HadCovid { get; set; }
    public string? CovidWhen { get; set; }
    public bool CovidVaccine { get; set; }

    // Vitals captured by the form; kept here so the next visit can prefill them.
    public float? Weight { get; set; }
    public float? Height { get; set; }

    public string? AdditionalHealthInfo { get; set; }
}
