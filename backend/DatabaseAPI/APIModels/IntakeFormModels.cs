namespace DatabaseAPI.APIModels;

using System.Text.Json.Serialization;

public class CreateIntakeFormEventRequest
{
    public int PatientId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    [JsonConverter(typeof(FlexibleNullableDateTimeConverter))]
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public float? Weight { get; set; }
    public float? Height { get; set; }
    public int? InsuranceNumber { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    
    // Medical data - will be stored in comment
    public bool? MedBloodPressure { get; set; }
    public bool? MedHeart { get; set; }
    public bool? MedCholesterol { get; set; }
    public bool? MedBloodThinners { get; set; }
    public bool? MedDiabetes { get; set; }
    public bool? MedThyroid { get; set; }
    public bool? MedNerves { get; set; }
    public bool? MedPsych { get; set; }
    public bool? MedDigestion { get; set; }
    public bool? MedPain { get; set; }
    public bool? MedDehydration { get; set; }
    public bool? MedBreathing { get; set; }
    public bool? MedAntibiotics { get; set; }
    public bool? MedSupplements { get; set; }
    public bool? MedAllergies { get; set; }
    
    public bool? PoorSleep { get; set; }
    public bool? DigestiveIssues { get; set; }
    public bool? PhysicalStress { get; set; }
    public bool? MentalStress { get; set; }
    public bool? Smoking { get; set; }
    public bool? Fatigue { get; set; }
    public string? LastMealHours { get; set; }
    
    public bool? HadCovid { get; set; }
    public string? CovidWhen { get; set; }
    public bool? CovidVaccine { get; set; }
    public bool? VaccinesAfter2023 { get; set; }
    
    public string? LastMenstruationDate { get; set; }
    public int? MenstruationCycleDays { get; set; }
    public int? YearsSinceLastMenstruation { get; set; }
    public bool? GaveBirth { get; set; }
    public int? BirthCount { get; set; }
    public string? BirthWhen { get; set; }
    public bool? Breastfed { get; set; }
    public int? BreastfeedingMonths { get; set; }
    public bool? BreastfeedingInflammation { get; set; }
    public bool? EndedWithInflammation { get; set; }
    public bool? Contraception { get; set; }
    public string? ContraceptionDuration { get; set; }
    public bool? Estrogen { get; set; }
    public string? EstrogenType { get; set; }
    public bool? Interruption { get; set; }
    public int? InterruptionCount { get; set; }
    public bool? Miscarriage { get; set; }
    public int? MiscarriageCount { get; set; }
    public bool? BreastInjury { get; set; }
    public bool? Mammogram { get; set; }
    public int? MammogramCount { get; set; }
    public bool? BreastBiopsy { get; set; }
    public bool? BreastImplants { get; set; }
    public bool? BreastSurgery { get; set; }
    public string? BreastSurgeryType { get; set; }
    public bool? FamilyTumors { get; set; }
    public string? FamilyTumorType { get; set; }

    public bool? ConfirmAccuracy { get; set; }
    public bool? TermsAccepted { get; set; }
    public string? SignaturePlace { get; set; }
    [JsonConverter(typeof(FlexibleNullableDateTimeConverter))]
    public DateTime? SignatureDate { get; set; }
    public string? SignatureVector { get; set; }

    public string? AdditionalHealthInfo { get; set; }

    public List<SicknessHistoryRequestItem> SicknessHistories { get; set; } = [];
}

public class SicknessHistoryRequestItem
{
    public string? SicknessName { get; set; }
    public bool? HadSickness { get; set; }
    public string? SicknessWhen { get; set; }
    public bool? Vaccinated { get; set; }
    public string? VaccinationWhen { get; set; }
    public string? Notes { get; set; }
}

public class IntakeFormEventDto
{
    public int EventId { get; set; }
    public float? Weight { get; set; }
    public float? Height { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PatientName { get; set; } = string.Empty;
}

