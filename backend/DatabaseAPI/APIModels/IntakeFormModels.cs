namespace DatabaseAPI.APIModels;

public class CreateIntakeFormEventRequest
{
    public int PatientId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
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
    public bool? CovidVaccine { get; set; }
    public bool? VaccinesAfter2023 { get; set; }
    
    public string? AdditionalHealthInfo { get; set; }
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

