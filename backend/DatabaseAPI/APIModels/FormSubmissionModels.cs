namespace DatabaseAPI.APIModels;

public class FormSubmissionMedicationDto
{
    public int Id { get; set; }
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

public class FormSubmissionLifestyleDto
{
    public int Id { get; set; }
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

public class FormSubmissionReproductiveHealthDto
{
    public int Id { get; set; }
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

public class SicknessHistoryDto
{
    public int Id { get; set; }
    public string SicknessName { get; set; } = string.Empty;
    public bool? HadSickness { get; set; }
    public string? SicknessWhen { get; set; }
    public bool? Vaccinated { get; set; }
    public string? VaccinationWhen { get; set; }
    public string? Notes { get; set; }
}

public class FormSubmissionConsentDto
{
    public int Id { get; set; }
    public bool ConfirmAccuracy { get; set; }
    public bool TermsAccepted { get; set; }
    public string? SignaturePlace { get; set; }
    public DateTime? SignatureDate { get; set; }
    public string? SignatureVector { get; set; }
}

public class FormSubmissionDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int EventId { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public FormSubmissionMedicationDto? Medication { get; set; }
    public FormSubmissionLifestyleDto? Lifestyle { get; set; }
    public FormSubmissionReproductiveHealthDto? ReproductiveHealth { get; set; }
    public FormSubmissionConsentDto? Consent { get; set; }
    public List<SicknessHistoryDto> SicknessHistories { get; set; } = new();
}
