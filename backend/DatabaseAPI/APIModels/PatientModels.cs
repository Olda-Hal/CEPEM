namespace DatabaseAPI.APIModels;

public class QuickPreviewSettingsDto
{
    public bool ShowCovidVaccination { get; set; } = true;
    public bool ShowFluVaccination { get; set; } = true;
    public bool ShowDiabetes { get; set; } = true;
    public bool ShowHypertension { get; set; } = true;  
    public bool ShowHeartDisease { get; set; } = true;
    public bool ShowAllergies { get; set; } = true;
    public bool ShowRecentEvents { get; set; } = true;
    public bool ShowUpcomingAppointments { get; set; } = true;
    public bool ShowLastVisit { get; set; } = true;
}

public class UpdateQuickPreviewSettingsRequest
{
    public bool ShowCovidVaccination { get; set; }
    public bool ShowFluVaccination { get; set; }
    public bool ShowDiabetes { get; set; }
    public bool ShowHypertension { get; set; }
    public bool ShowHeartDisease { get; set; }
    public bool ShowAllergies { get; set; }
    public bool ShowRecentEvents { get; set; }
    public bool ShowUpcomingAppointments { get; set; }
    public bool ShowLastVisit { get; set; }
}

public class PatientDto
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int InsuranceNumber { get; set; }
    public string Gender { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string UID { get; set; } = string.Empty;
    public string? TitleBefore { get; set; }
    public string? TitleAfter { get; set; }
    public bool Alive { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
}

public class PatientSearchRequest
{
    public int Page { get; set; } = 0;
    public int Limit { get; set; } = 20;
    public string Search { get; set; } = string.Empty;
    public string SortBy { get; set; } = "LastName";
    public string SortOrder { get; set; } = "asc";
}

public class PatientSearchResponse
{
    public List<PatientDto> Patients { get; set; } = new();
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}

public class CreatePatientRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int InsuranceNumber { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string? TitleBefore { get; set; }
    public string? TitleAfter { get; set; }
}

public class PatientQuickPreviewDto
{
    public bool HasCovidVaccination { get; set; }
    public bool HasFluVaccination { get; set; }
    public bool HasDiabetes { get; set; }
    public bool HasHypertension { get; set; }
    public bool HasHeartDisease { get; set; }
    public bool HasAllergies { get; set; }
    public int RecentEventsCount { get; set; }
    public int UpcomingAppointmentsCount { get; set; }
    public DateTime? LastVisit { get; set; }
    public string? LastVisitType { get; set; }
}

public class PatientDetailDto
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int InsuranceNumber { get; set; }
    public string Gender { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string UID { get; set; } = string.Empty;
    public string? TitleBefore { get; set; }
    public string? TitleAfter { get; set; }
    public bool Alive { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Comment { get; set; }
    public string? PhotoUrl { get; set; }
    public PatientQuickPreviewDto QuickPreview { get; set; } = new();
    public QuickPreviewSettingsDto QuickPreviewSettings { get; set; } = new();
    public List<PatientEventDto> Events { get; set; } = new();
    public List<PatientAppointmentDto> Appointments { get; set; } = new();
    public List<PatientDocumentDto> Documents { get; set; } = new();
}

public class PatientEventDto
{
    public int Id { get; set; }
    public string EventTypeName { get; set; } = string.Empty;
    public DateTime HappenedAt { get; set; }
    public DateTime? HappenedTo { get; set; }
    public string? Comment { get; set; }
    public List<string> DrugUses { get; set; } = new();
    public List<string> Examinations { get; set; } = new();
    public List<string> Symptoms { get; set; } = new();
    public List<string> Injuries { get; set; } = new();
    public List<string> Vaccines { get; set; } = new();
    public bool HasPregnancy { get; set; }
}

public class PatientAppointmentDto
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? EquipmentName { get; set; }
    public string HospitalName { get; set; } = string.Empty;
}

public class UpdateCommentRequest
{
    public string Comment { get; set; } = string.Empty;
}
