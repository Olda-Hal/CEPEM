using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class SicknessHistory
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string SicknessName { get; set; } = string.Empty;

    public bool? HadSickness { get; set; }
    public string? SicknessWhen { get; set; }

    public bool? Vaccinated { get; set; }
    public string? VaccinationWhen { get; set; }

    public string? Notes { get; set; }

    public int FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;
}
