using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class FormSubmission
{
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public FormSubmissionMedication? Medication { get; set; }
    public FormSubmissionLifestyle? Lifestyle { get; set; }
    public FormSubmissionReproductiveHealth? ReproductiveHealth { get; set; }
    public FormSubmissionConsent? Consent { get; set; }
    public ICollection<SicknessHistory> SicknessHistories { get; set; } = [];
}
