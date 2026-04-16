namespace DatabaseAPI.DatabaseModels;

public class FormSubmissionReproductiveHealth
{
    public int Id { get; set; }

    public int FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;

    public string? LastMenstruationDate { get; set; }
    public int? MenstruationCycleDays { get; set; }
    public int? YearsSinceLastMenstruation { get; set; }

    public bool GaveBirth { get; set; }
    public int? BirthCount { get; set; }
    public string? BirthWhen { get; set; }

    public bool Breastfed { get; set; }
    public int? BreastfeedingMonths { get; set; }
    public bool BreastfeedingInflammation { get; set; }
    public bool EndedWithInflammation { get; set; }

    public bool Contraception { get; set; }
    public string? ContraceptionDuration { get; set; }

    public bool Estrogen { get; set; }
    public string? EstrogenType { get; set; }

    public bool Interruption { get; set; }
    public int? InterruptionCount { get; set; }

    public bool Miscarriage { get; set; }
    public int? MiscarriageCount { get; set; }

    public bool BreastInjury { get; set; }
    public bool Mammogram { get; set; }
    public int? MammogramCount { get; set; }

    public bool BreastBiopsy { get; set; }
    public bool BreastImplants { get; set; }

    public bool BreastSurgery { get; set; }
    public string? BreastSurgeryType { get; set; }

    public bool FamilyTumors { get; set; }
    public string? FamilyTumorType { get; set; }
}
